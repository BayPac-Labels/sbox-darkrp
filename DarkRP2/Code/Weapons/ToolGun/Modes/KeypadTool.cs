using System.Collections.Concurrent;
using Sandbox.UI;

[Icon( "dialpad" )]
[Title( "#tool.name.keypad" )]
[ClassName( "keypad" )]
[Group( "#tool.group.tools" )]
public sealed class KeypadTool : ToolMode
{
	public override bool UseSnapGrid => false;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, Sync, Title( "Access Password" )]
	public string Password { get; set; } = "1337";

	[Property, Sync, Title( "Secure Mode" )]
	public bool Secure { get; set; } = false;

	[Property, Sync, KeyBind, Title( "Access Granted Key" )]
	public string AccessGrantedKey { get; set; } = "5";

	[Property, Sync, KeyBind, Title( "Access Denied Key" )]
	public string AccessDeniedKey { get; set; } = "";

	[Property, Sync, Title( "Hold Length Granted" )]
	public float LengthGranted { get; set; } = 4f;

	[Property, Sync, Title( "Hold Length Denied" )]
	public float LengthDenied { get; set; } = 0.1f;

	const string DefaultKeypadModel = "models/keypad/keypad.vmdl";

	[Property, Title( "Model" ), WireModel( "controller", "button" )]
	public string SpawnModel { get; set; } = DefaultKeypadModel;

	public override string Description => "#tool.hint.keypad.description";

	static readonly ConcurrentDictionary<string, Model> SpawnModelCache = new( StringComparer.OrdinalIgnoreCase );
	static readonly ConcurrentDictionary<string, byte> CloudLoadsInFlight = new( StringComparer.OrdinalIgnoreCase );

	protected override void OnStart()
	{
		WireCloudModels.EnsureBundled();
		base.OnStart();

		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = DefaultKeypadModel;

		RegisterAction( ToolInput.Primary, () => "#tool.hint.keypad.place", OnPlace );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.keypad.update", OnUpdateExisting );
	}

	public override void OnControl()
	{
		base.OnControl();

		KeyBindControl.Active?.PollWhileListening();

		var select = TraceSelect();
		if ( !select.IsValid() || string.IsNullOrWhiteSpace( SpawnModel ) )
			return;

		var model = LoadSpawnModel( SpawnModel );
		if ( model is null )
		{
			EnsureCloudModel( SpawnModel );
			return;
		}

		Game.ActiveScene.DebugOverlay.Model( model, transform: GetPlacement( select ), overlay: false );
	}

	void OnPlace()
	{
		var select = TraceSelect();
		if ( !select.IsValid() )
			return;

		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			return;

		var tx = GetPlacement( select );
		_ = SpawnResolved( select, SpawnModel, tx );
	}

	void OnUpdateExisting()
	{
		var select = TraceSelect();
		if ( !select.IsValid() || select.IsWorld )
			return;

		var keypad = select.GameObject.GetComponent<KeypadComponent>()
			?? select.GameObject.GetComponentInParent<KeypadComponent>()
			?? select.GameObject.GetComponentInChildren<KeypadComponent>();
		if ( !keypad.IsValid() )
			return;

		UpdateKeypad( keypad.GameObject, Password, Secure, AccessGrantedKey, AccessDeniedKey, LengthGranted, LengthDenied );
		ShootEffects( select );
	}

	/// <summary>
	/// Keypad face is on model -X. Aim Forward into the wall so -X faces the player.
	/// Use -Up so the baked face (screen at +Z) sits upright on vertical walls.
	/// </summary>
	Transform GetPlacement( SelectionPoint select )
	{
		var pos = select.WorldTransform();
		var outward = pos.Rotation.Forward;
		var intoWall = -outward;
		var up = MathF.Abs( Vector3.Dot( intoWall, Vector3.Up ) ) > 0.99f
			? Vector3.Forward
			: Vector3.Up;
		return new Transform( pos.Position, Rotation.LookAt( intoWall, -up ) );
	}

	async Task SpawnResolved( SelectionPoint select, string modelPath, Transform tx )
	{
		var model = await ResolveSpawnModel( modelPath );
		if ( model is null )
			return;

		Spawn( select, modelPath, tx, Password, Secure, AccessGrantedKey, AccessDeniedKey, LengthGranted, LengthDenied );
		ShootEffects( select );
	}

	[Rpc.Host]
	void Spawn( SelectionPoint point, string modelPath, Transform tx, string password, bool secure, string grantedKey, string deniedKey, float lengthGranted, float lengthDenied )
	{
		_ = SpawnAsync( point, modelPath, tx, password, secure, grantedKey, deniedKey, lengthGranted, lengthDenied );
	}

	async Task SpawnAsync( SelectionPoint point, string modelPath, Transform tx, string password, bool secure, string grantedKey, string deniedKey, float lengthGranted, float lengthDenied )
	{
		if ( !CanUseToolOn( point ) )
			return;
		if ( !KeypadComponent.IsValidPassword( password ) )
		{
			Player?.SendToolActionDeniedNotice( "Invalid password (digits 1-9, max 4)." );
			return;
		}
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return;

		var model = await ResolveSpawnModel( modelPath );
		if ( model is null )
			return;

		if ( !TryUseToolSpawnLimit() )
			return;
		if ( !TryUseToolActionCooldown() )
			return;

		var go = new GameObject( false, "keypad" );
		go.Tags.Add( "removable" );
		go.WorldTransform = tx;

		var prop = go.AddComponent<Prop>();
		prop.Model = model;

		if ( (model.Physics?.Parts?.Count ?? 0) == 0 )
		{
			var collider = go.AddComponent<BoxCollider>();
			collider.Scale = model.Bounds.Size;
			collider.Center = model.Bounds.Center;
			go.AddComponent<Rigidbody>();
		}

		var keypad = go.AddComponent<KeypadComponent>();
		keypad.Configure( password, secure, grantedKey, deniedKey, lengthGranted, lengthDenied );

		var welded = false;
		if ( !point.IsWorld )
		{
			var joint = go.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.WithScale( 1 ).ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
			welded = true;
		}

		ApplyPhysicsProperties( go );
		RegisterToolSpawnedObject( go );
		go.NetworkSpawn( true, null );

		if ( !welded )
		{
			foreach ( var rb in go.GetComponentsInChildren<Rigidbody>( true ) )
				rb.MotionEnabled = false;
		}

		Track( go );

		var undo = Player.Undo.Create();
		undo.Name = "Keypad";
		undo.Icon = "dialpad";
		undo.Add( go );
	}

	[Rpc.Host]
	void UpdateKeypad( GameObject go, string password, bool secure, string grantedKey, string deniedKey, float lengthGranted, float lengthDenied )
	{
		if ( !go.IsValid() || go.IsProxy )
			return;
		if ( !CanUseToolOn( go ) )
			return;
		if ( !KeypadComponent.IsValidPassword( password ) )
		{
			Player?.SendToolActionDeniedNotice( "Invalid password (digits 1-9, max 4)." );
			return;
		}
		if ( !TryUseToolActionCooldown() )
			return;

		var keypad = go.GetComponent<KeypadComponent>() ?? go.GetComponentInChildren<KeypadComponent>();
		if ( !keypad.IsValid() )
			return;

		keypad.Configure( password, secure, grantedKey, deniedKey, lengthGranted, lengthDenied );
	}

	static bool IsCloudIdent( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		return !modelPath.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase )
			&& !modelPath.EndsWith( ".vmdl_c", StringComparison.OrdinalIgnoreCase );
	}

	static Model LoadSpawnModel( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return null;

		if ( SpawnModelCache.TryGetValue( modelPath, out var cached ) && cached.IsValid() && !cached.IsError )
			return cached;

		if ( !IsCloudIdent( modelPath ) )
		{
			var local = Model.Load( modelPath );
			if ( local.IsValid() && !local.IsError )
			{
				SpawnModelCache[modelPath] = local;
				return local;
			}
		}

		var bundled = WireCloudModels.TryResolve( modelPath );
		if ( bundled is not null )
		{
			SpawnModelCache[modelPath] = bundled;
			return bundled;
		}

		return null;
	}

	static async Task<Model> ResolveSpawnModel( string modelPath )
	{
		var model = LoadSpawnModel( modelPath );
		if ( model is not null )
			return model;

		var ident = WireCloudModels.CloudIdentFor( modelPath );
		if ( string.IsNullOrWhiteSpace( ident ) )
			return null;

		var loaded = await Cloud.Load<Model>( ident );
		if ( loaded is not null && loaded.IsValid() && !loaded.IsError )
		{
			SpawnModelCache[modelPath] = loaded;
			SpawnModelCache[ident] = loaded;
			return loaded;
		}

		return null;
	}

	static void EnsureCloudModel( string modelPath )
	{
		if ( LoadSpawnModel( modelPath ) is not null )
			return;

		if ( string.IsNullOrWhiteSpace( WireCloudModels.CloudIdentFor( modelPath ) ) )
			return;

		if ( !CloudLoadsInFlight.TryAdd( modelPath, 0 ) )
			return;

		_ = WarmCloudModel( modelPath );
	}

	static async Task WarmCloudModel( string modelPath )
	{
		try
		{
			await ResolveSpawnModel( modelPath );
		}
		finally
		{
			CloudLoadsInFlight.TryRemove( modelPath, out _ );
		}
	}
}
