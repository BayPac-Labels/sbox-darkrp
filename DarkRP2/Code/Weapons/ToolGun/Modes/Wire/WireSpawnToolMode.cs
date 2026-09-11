public abstract class WireSpawnToolMode : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, Title( "Model" )]
	public string SpawnModel { get; set; }

	protected abstract void AddWireComponent( GameObject go );
	protected virtual string UndoName => TypeDescription?.Title ?? "Wire";
	protected virtual string UndoIcon => TypeDescription?.Icon ?? "⚡";
	protected virtual bool RegisterNoWeldSecondary => true;

	protected override void OnStart()
	{
		base.OnStart();
		RegisterAction( ToolInput.Primary, () => "#tool.hint.wire.place", OnPlace );
		if ( RegisterNoWeldSecondary )
			RegisterAction( ToolInput.Secondary, () => "#tool.hint.wire.place_no_weld", OnPlaceNoWeld );
	}

	protected void Place( bool weld ) => TrySpawn( weld );

	void OnPlace() => TrySpawn( true );
	void OnPlaceNoWeld() => TrySpawn( false );

	void TrySpawn( bool weld )
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;
		if ( string.IsNullOrWhiteSpace( SpawnModel ) ) return;

		Spawn( select, SpawnModel, GetPlacement( select ), weld );
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

		var model = Model.Load( SpawnModel );
		if ( !model.IsValid() ) return;

		Game.ActiveScene.DebugOverlay.Model( model, transform: GetPlacement( select ), overlay: false );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, string modelPath, Transform tx, bool weld )
	{
		if ( !CanUseToolOn( point ) ) return;
		if ( string.IsNullOrWhiteSpace( modelPath ) ) return;
		if ( !TryUseToolSpawnLimit() ) return;
		if ( !TryUseToolActionCooldown() ) return;

		var model = Model.Load( modelPath );
		if ( !model.IsValid() ) return;

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
		}

		ApplyPhysicsProperties( go );
		RegisterToolSpawnedObject( go );
		go.NetworkSpawn( true, null );
		Track( go );

		var undo = Player.Undo.Create();
		undo.Name = UndoName;
		undo.Icon = UndoIcon;
		undo.Add( go );
	}
}
