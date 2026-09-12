using System.Collections.Concurrent;
using Sandbox.UI;

public abstract class WireSpawnToolMode : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, Title( "Model" ), WireModel( "controller" )]
	public virtual string SpawnModel { get; set; }

	protected abstract void AddWireComponent( GameObject go );
	protected virtual string UndoName => TypeDescription?.Title ?? "Wire";
	protected virtual string UndoIcon => TypeDescription?.Icon ?? "⚡";
	protected virtual bool RegisterNoWeldSecondary => true;

	static readonly ConcurrentDictionary<string, Model> SpawnModelCache = new( StringComparer.OrdinalIgnoreCase );
	static readonly ConcurrentDictionary<string, byte> CloudLoadsInFlight = new( StringComparer.OrdinalIgnoreCase );

	protected override void OnStart()
	{
		WireCloudModels.EnsureBundled();
		base.OnStart();
		RegisterAction( ToolInput.Primary, () => "#tool.hint.wire.place", OnPlace );
		if ( RegisterNoWeldSecondary )
			RegisterAction( ToolInput.Secondary, () => "#tool.hint.wire.place_no_weld", OnPlaceNoWeld );
	}

	protected static bool IsCloudIdent( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		return !modelPath.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase )
			&& !modelPath.EndsWith( ".vmdl_c", StringComparison.OrdinalIgnoreCase );
	}

	protected static Model LoadSpawnModel( string modelPath )
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

	protected static async Task<Model> ResolveSpawnModel( string modelPath )
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

	protected void Place( bool weld ) => TrySpawn( weld );

	void OnPlace() => TrySpawn( true );
	void OnPlaceNoWeld() => TrySpawn( false );

	void TrySpawn( bool weld )
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;
		if ( string.IsNullOrWhiteSpace( SpawnModel ) ) return;

		var tx = GetPlacement( select );
		_ = SpawnResolved( select, SpawnModel, tx, weld );
	}

	async Task SpawnResolved( SelectionPoint select, string modelPath, Transform tx, bool weld )
	{
		var model = await ResolveSpawnModel( modelPath );
		if ( model is null ) return;

		Spawn( select, modelPath, tx, weld );
		ShootEffects( select );
	}

	protected virtual Transform GetPlacement( SelectionPoint select )
	{
		var pos = select.WorldTransform();
		return new Transform( pos.Position, pos.Rotation * new Angles( 90, 0, 0 ) );
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() || string.IsNullOrWhiteSpace( SpawnModel ) ) return;

		var model = LoadSpawnModel( SpawnModel );
		if ( model is null )
		{
			EnsureCloudModel( SpawnModel );
			return;
		}

		Game.ActiveScene.DebugOverlay.Model( model, transform: GetPlacement( select ), overlay: false );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, string modelPath, Transform tx, bool weld )
	{
		_ = SpawnInternalAsync( point, modelPath, tx, weld );
	}

	protected void SpawnInternal( SelectionPoint point, string modelPath, Transform tx, bool weld )
	{
		_ = SpawnInternalAsync( point, modelPath, tx, weld );
	}

	protected async Task SpawnInternalAsync( SelectionPoint point, string modelPath, Transform tx, bool weld )
	{
		if ( !CanUseToolOn( point ) ) return;
		if ( string.IsNullOrWhiteSpace( modelPath ) ) return;

		var model = await ResolveSpawnModel( modelPath );
		if ( model is null ) return;

		if ( !TryUseToolSpawnLimit() ) return;
		if ( !TryUseToolActionCooldown() ) return;

		var go = new GameObject( false, "wire" );
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

		AddWireComponent( go );

		var welded = false;
		if ( weld && !point.IsWorld )
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
		undo.Name = UndoName;
		undo.Icon = UndoIcon;
		undo.Add( go );
	}
}
