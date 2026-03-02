using Godot;
using System;

public partial class NewScript : Node3D
{
	[Export] public int WallRows { get; set; } = 8;
	[Export] public int WallColumns { get; set; } = 10;
	[Export] public int WallDepth { get; set; } = 2;
	[Export] public float CannonImpulse { get; set; } = 180.0f;
	[Export] public Vector3 BrickSize { get; set; } = new Vector3(1.2f, 0.6f, 0.6f);
	[Export] public float BrickGap { get; set; } = 0.04f;
	[Export] public float CannonBallRadius { get; set; } = 0.35f;
	[Export] public float CannonBallMass { get; set; } = 1.5f;
	[Export] public float CameraMoveSpeed { get; set; } = 10.0f;
	[Export] public float CameraFastMultiplier { get; set; } = 2.0f;
	[Export] public float CameraLookSensitivity { get; set; } = 0.0025f;
	[Export] public string BrickTexturePath { get; set; } = "res://brick_single_albedo_natural.png";
	[Export] public Vector3 BrickTextureScale { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);
	[Export] public string FloorTexturePath { get; set; } = "res://ground_natural_earth_seamless.png";
	[Export] public Vector3 FloorTextureScale { get; set; } = new Vector3(8.0f, 8.0f, 1.0f);

	private Marker3D _launcher = null!;
	private Marker3D _wallOrigin = null!;
	private Camera3D _camera = null!;
	private StandardMaterial3D _brickMaterial = null!;
	private StandardMaterial3D _ballMaterial = null!;
	private Node3D _wallContainer = null!;
	private float _yaw;
	private float _pitch;

	public override void _Ready()
	{
		_launcher = GetNode<Marker3D>("Launcher");
		_wallOrigin = GetNode<Marker3D>("WallOrigin");
		_camera = GetNode<Camera3D>("Camera3D");
		_yaw = _camera.Rotation.Y;
		_pitch = _camera.Rotation.X;
		Input.MouseMode = Input.MouseModeEnum.Captured;

		_brickMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.75f, 0.28f, 0.22f),
			Roughness = 0.95f
		};
		Texture2D? brickTexture = TryLoadBrickTexture();
		if (brickTexture != null)
		{
			_brickMaterial.AlbedoTexture = brickTexture;
			_brickMaterial.Uv1Scale = BrickTextureScale;
		}

		_ballMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.12f, 0.12f, 0.12f),
			Metallic = 0.35f,
			Roughness = 0.3f
		};

		ApplyFloorMaterial();

		BuildWall();
	}

	private Texture2D? TryLoadBrickTexture()
	{
		if (!string.IsNullOrWhiteSpace(BrickTexturePath) && ResourceLoader.Exists(BrickTexturePath))
		{
			return GD.Load<Texture2D>(BrickTexturePath);
		}

		return null;
	}

	private Texture2D? TryLoadFloorTexture()
	{
		if (!string.IsNullOrWhiteSpace(FloorTexturePath) && ResourceLoader.Exists(FloorTexturePath))
		{
			return GD.Load<Texture2D>(FloorTexturePath);
		}

		return null;
	}

	private void ApplyFloorMaterial()
	{
		MeshInstance3D? floorMesh = GetNodeOrNull<MeshInstance3D>("Ground/MeshInstance3D");
		if (floorMesh == null)
		{
			return;
		}

		StandardMaterial3D floorMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.72f, 0.68f, 0.60f),
			Roughness = 0.95f
		};

		Texture2D? floorTexture = TryLoadFloorTexture();
		if (floorTexture != null)
		{
			floorMaterial.AlbedoTexture = floorTexture;
			floorMaterial.Uv1Scale = FloorTextureScale;
		}

		floorMesh.MaterialOverride = floorMaterial;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_yaw -= mouseMotion.Relative.X * CameraLookSensitivity;
			_pitch -= mouseMotion.Relative.Y * CameraLookSensitivity;
			_pitch = Mathf.Clamp(_pitch, -1.2f, 1.2f);
			_camera.Rotation = new Vector3(_pitch, _yaw, 0.0f);
		}

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Space)
		{
			LaunchCannonBall();
		}

		if (@event is InputEventKey resetEvent && resetEvent.Pressed && !resetEvent.Echo && resetEvent.Keycode == Key.R)
		{
			ResetWall();
		}

		if (@event is InputEventKey escapeEvent && escapeEvent.Pressed && !escapeEvent.Echo && escapeEvent.Keycode == Key.Escape)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 moveInput = Vector3.Zero;
		if (Input.IsKeyPressed(Key.W)) moveInput.Z += 1.0f;
		if (Input.IsKeyPressed(Key.S)) moveInput.Z -= 1.0f;
		if (Input.IsKeyPressed(Key.A)) moveInput.X -= 1.0f;
		if (Input.IsKeyPressed(Key.D)) moveInput.X += 1.0f;
		if (Input.IsKeyPressed(Key.E)) moveInput.Y += 1.0f;
		if (Input.IsKeyPressed(Key.Q)) moveInput.Y -= 1.0f;

		if (moveInput == Vector3.Zero)
		{
			return;
		}

		float speed = CameraMoveSpeed * (float)delta;
		if (Input.IsKeyPressed(Key.Shift))
		{
			speed *= CameraFastMultiplier;
		}

		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		Vector3 right = _camera.GlobalTransform.Basis.X;
		Vector3 up = _camera.GlobalTransform.Basis.Y;
		Vector3 movement = (right * moveInput.X) + (up * moveInput.Y) + (forward * moveInput.Z);
		_camera.GlobalPosition += movement.Normalized() * speed;
	}

	private void BuildWall()
	{
		_wallContainer = new Node3D();
		_wallContainer.Name = "BrickWall";
		AddChild(_wallContainer);

		float stepX = BrickSize.X + BrickGap;
		float stepY = BrickSize.Y + BrickGap;
		float stepZ = BrickSize.Z + BrickGap;
		float wallWidth = (WallColumns - 1) * stepX;
		float wallDepth = (WallDepth - 1) * stepZ;

		for (int y = 0; y < WallRows; y++)
		{
			float rowOffset = (y % 2 == 0) ? 0.0f : stepX * 0.5f;

			for (int z = 0; z < WallDepth; z++)
			{
				for (int x = 0; x < WallColumns; x++)
				{
					Vector3 localPos = new Vector3(
						(x * stepX) - (wallWidth * 0.5f) + rowOffset,
						(y * stepY) + (BrickSize.Y * 0.5f),
						(z * stepZ) - (wallDepth * 0.5f)
					);

					RigidBody3D brick = CreateBrick(_wallOrigin.Position + localPos);
					_wallContainer.AddChild(brick);
				}
			}
		}
	}

	private RigidBody3D CreateBrick(Vector3 position)
	{
		RigidBody3D brick = new RigidBody3D();
		brick.Position = position;
		brick.Mass = 1.0f;
		brick.ContinuousCd = true;

		CollisionShape3D collider = new CollisionShape3D();
		collider.Shape = new BoxShape3D { Size = BrickSize };
		brick.AddChild(collider);

		MeshInstance3D mesh = new MeshInstance3D();
		mesh.Mesh = new BoxMesh { Size = BrickSize };
		mesh.MaterialOverride = _brickMaterial;
		brick.AddChild(mesh);

		return brick;
	}

	private void LaunchCannonBall()
	{
		Vector3 spawnPosition = _launcher.GlobalPosition;
		Vector3 direction = (_camera.GlobalPosition - spawnPosition).Normalized();

		RigidBody3D cannonBall = new RigidBody3D();
		cannonBall.Name = $"CannonBall{Time.GetTicksMsec()}";
		cannonBall.GlobalPosition = spawnPosition;
		cannonBall.Mass = CannonBallMass;
		cannonBall.ContinuousCd = true;
		cannonBall.AddToGroup("projectile");

		CollisionShape3D collider = new CollisionShape3D();
		collider.Shape = new SphereShape3D { Radius = CannonBallRadius };
		cannonBall.AddChild(collider);

		MeshInstance3D mesh = new MeshInstance3D();
		mesh.Mesh = new SphereMesh
		{
			Radius = CannonBallRadius,
			Height = CannonBallRadius * 2.0f
		};
		mesh.MaterialOverride = _ballMaterial;
		cannonBall.AddChild(mesh);

		AddChild(cannonBall);
		cannonBall.ApplyCentralImpulse(direction * CannonImpulse);
	}

	private void ResetWall()
	{
		if (IsInstanceValid(_wallContainer))
		{
			_wallContainer.QueueFree();
		}

		foreach (Node node in GetTree().GetNodesInGroup("projectile"))
		{
			node.QueueFree();
		}

		BuildWall();
	}
}
