using Sandbox.Rendering;

[Icon( "cable" )]
[Title( "#tool.name.wiring" )]
[ClassName( "wiring" )]
[Group( "#tool.group.wire" )]
public class WiringTool : ToolMode
{
	GameObject _inputEnt;
	int _inputPortIndex;
	int _outputPortIndex;

	public override string Description => _inputEnt.IsValid()
		? "#tool.hint.wiring.stage1"
		: "#tool.hint.wiring.stage0";

	protected override void OnStart()
	{
		base.OnStart();
		RegisterAction( ToolInput.Primary, () => _inputEnt.IsValid() ? "#tool.hint.wiring.select_output" : "#tool.hint.wiring.select_input", OnPrimary );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.wiring.next_port", OnCycle );
		RegisterAction( ToolInput.Reload, () => _inputEnt.IsValid() ? "#tool.hint.wiring.cancel" : "#tool.hint.wiring.disconnect", OnReload );
	}

	void OnPrimary()
	{
		var select = TraceSelect();
		if ( !select.IsValid() || select.IsWorld ) return;

		if ( !_inputEnt.IsValid() )
		{
			if ( select.GameObject.GetComponent<IWireInputComponent>() is not IWireInputComponent input || input.GetInputNames().Length == 0 )
				return;

			_inputEnt = select.GameObject;
			_inputPortIndex = 0;
			_outputPortIndex = 0;
			ShootEffects( select );
			return;
		}

		if ( _inputEnt.GetComponent<IWireInputComponent>() is not IWireInputComponent wireInput )
		{
			_inputEnt = null;
			return;
		}

		if ( select.GameObject.GetComponent<IWireOutputComponent>() is not IWireOutputComponent wireOutput )
			return;

		var inputNames = wireInput.GetInputNames();
		var outputNames = wireOutput.GetOutputNames();
		if ( inputNames.Length == 0 || outputNames.Length == 0 ) return;

		_inputPortIndex = Math.Clamp( _inputPortIndex, 0, inputNames.Length - 1 );
		_outputPortIndex = Math.Clamp( _outputPortIndex, 0, outputNames.Length - 1 );
		wireOutput.WireConnect( _inputEnt, outputNames[_outputPortIndex], inputNames[_inputPortIndex] );
		ShootEffects( select );
		_inputEnt = null;
	}

	void OnCycle()
	{
		var delta = Input.Down( "run" ) ? -1 : 1;
		if ( _inputEnt.IsValid() )
			_outputPortIndex += delta;
		else
			_inputPortIndex += delta;
	}

	void OnReload()
	{
		if ( _inputEnt.IsValid() )
		{
			_inputEnt = null;
			return;
		}

		var select = TraceSelect();
		if ( !select.IsValid() ) return;
		if ( select.GameObject.GetComponent<IWireInputComponent>() is not IWireInputComponent input )
			return;

		var names = input.GetInputNames();
		if ( names.Length == 0 ) return;
		input.DisconnectInput( names[Math.Clamp( _inputPortIndex, 0, names.Length - 1 )] );
		ShootEffects( select );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		base.DrawHud( painter, crosshair );

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		if ( _inputEnt.IsValid() )
		{
			if ( select.GameObject.GetComponent<IWireOutputComponent>() is not IWireOutputComponent output )
				return;

			var names = output.GetOutputNames();
			if ( names.Length == 0 ) return;
			_outputPortIndex = Math.Clamp( _outputPortIndex, 0, names.Length - 1 );
			Gizmo.Draw.ScreenText( $"Out: {names[_outputPortIndex]}", crosshair + new Vector2( 24, -18 ) );
			return;
		}

		if ( select.GameObject.GetComponent<IWireInputComponent>() is not IWireInputComponent input )
			return;

		var inputNames = input.GetInputNames();
		if ( inputNames.Length == 0 ) return;
		_inputPortIndex = Math.Clamp( _inputPortIndex, 0, inputNames.Length - 1 );
		Gizmo.Draw.ScreenText( $"In: {inputNames[_inputPortIndex]}", crosshair + new Vector2( 24, -18 ) );
	}
}

[Icon( "radio_button_checked" )]
[Title( "#tool.name.wirebutton" )]
[ClassName( "wirebutton" )]
[Group( "#tool.group.wire" )]
public class WireButtonTool : WireSpawnToolMode
{
	[Property, Sync, Title( "Toggle" )]
	public bool IsToggle { get; set; }

	public override string Description => "#tool.hint.wirebutton.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/button.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go )
	{
		go.AddComponent<WireButtonComponent>().IsToggle = IsToggle;
	}
}

[Icon( "my_location" )]
[Title( "#tool.name.wiregps" )]
[ClassName( "wiregps" )]
[Group( "#tool.group.wire" )]
public class WireGpsTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wiregps.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/apc.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireGPSComponent>();
}

[Icon( "fitness_center" )]
[Title( "#tool.name.wireweight" )]
[ClassName( "wireweight" )]
[Group( "#tool.group.wire" )]
public class WireWeightTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wireweight.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/apc.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireWeightComponent>();
}

[Icon( "speed" )]
[Title( "#tool.name.wirespeedometer" )]
[ClassName( "wirespeedometer" )]
[Group( "#tool.group.wire" )]
public class WireSpeedometerTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wirespeedometer.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/apc.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireSpeedometerComponent>();
}

[Icon( "explore" )]
[Title( "#tool.name.wiregyroscope" )]
[ClassName( "wiregyroscope" )]
[Group( "#tool.group.wire" )]
public class WireGyroscopeTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wiregyroscope.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/gyroscope.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireGyroscopeComponent>();
}

[Icon( "straighten" )]
[Title( "#tool.name.wireranger" )]
[ClassName( "wireranger" )]
[Group( "#tool.group.wire" )]
public class WireRangerTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wireranger.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/apc.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireRangerComponent>();
}

[Icon( "fast_forward" )]
[Title( "#tool.name.wireforcer" )]
[ClassName( "wireforcer" )]
[Group( "#tool.group.wire" )]
public class WireForcerTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wireforcer.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/apc.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireForcerComponent>();
}

[Icon( "memory" )]
[Title( "#tool.name.wiregate" )]
[ClassName( "wiregate" )]
[Group( "#tool.group.wire" )]
public class WireGateTool : WireSpawnToolMode
{
	[Property, Sync, Title( "Gate Type" )]
	public string GateType { get; set; } = "Add";

	public override string Description => "#tool.hint.wiregate.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/chip_rectangle.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go )
	{
		go.AddComponent<WireGateComponent>().GateType = string.IsNullOrWhiteSpace( GateType ) ? "Add" : GateType;
	}
}

[Icon( "monitor" )]
[Title( "#tool.name.wiredigitalscreen" )]
[ClassName( "wiredigitalscreen" )]
[Group( "#tool.group.wire" )]
public class WireDigitalScreenTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wiredigitalscreen.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/television/flatscreen_tv.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireDigitalScreenComponent>();
}

[Icon( "videocam" )]
[Title( "#tool.name.wirecamerascreen" )]
[ClassName( "wirecamerascreen" )]
[Group( "#tool.group.wire" )]
public class WireCameraScreenTool : WireSpawnToolMode
{
	[Property, Title( "Camera Model" )]
	public string CameraModel { get; set; } = "models/wirebox/katlatze/apc.vmdl";

	protected override bool RegisterNoWeldSecondary => false;
	public override string Description => "#tool.hint.wirecamerascreen.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/television/flatscreen_tv.vmdl";
		base.OnStart();
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.wirecamerascreen.place_camera", OnPlaceCamera );
	}

	protected override void AddWireComponent( GameObject go )
	{
		var screen = go.AddComponent<WireCameraScreenComponent>();
		var renderer = go.GetComponent<ModelRenderer>();
		if ( renderer.IsValid() )
			screen.OnNewModel( renderer.Model );
	}

	void OnPlaceCamera()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;
		if ( string.IsNullOrWhiteSpace( CameraModel ) ) return;

		SpawnCamera( select, CameraModel, GetPlacement( select ) );
		ShootEffects( select );
	}

	[Rpc.Host]
	void SpawnCamera( SelectionPoint point, string modelPath, Transform tx )
	{
		if ( !CanUseToolOn( point ) ) return;
		if ( string.IsNullOrWhiteSpace( modelPath ) ) return;
		if ( !TryUseToolSpawnLimit() ) return;
		if ( !TryUseToolActionCooldown() ) return;

		var model = Model.Load( modelPath );
		if ( !model.IsValid() )
			model = Model.Load( "models/wirebox/katlatze/apc.vmdl" );
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

		go.AddComponent<WireCameraComponent>();

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
		}

		ApplyPhysicsProperties( go );
		RegisterToolSpawnedObject( go );
		go.NetworkSpawn( true, null );
		Track( go );

		var undo = Player.Undo.Create();
		undo.Name = "Wire Camera";
		undo.Icon = "videocam";
		undo.Add( go );
	}
}

[Icon( "keyboard" )]
[Title( "#tool.name.wirekeyboard" )]
[ClassName( "wirekeyboard" )]
[Group( "#tool.group.wire" )]
public class WireKeyboardTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wirekeyboard.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/button.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireKeyboardComponent>();
}

[Icon( "view_stream" )]
[Title( "#tool.name.wirelightbridge" )]
[ClassName( "wirelightbridge" )]
[Group( "#tool.group.wire" )]
public class WireLightBridgeTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wirelightbridge.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/lightbridge.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go ) => go.AddComponent<WireLightBridgeComponent>();
}

[Icon( "scale" )]
[Title( "#tool.name.wireweightscale" )]
[ClassName( "wireweightscale" )]
[Group( "#tool.group.wire" )]
public class WireWeightScaleTool : WireSpawnToolMode
{
	public override string Description => "#tool.hint.wireweightscale.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/sbox_props/pallet/pallet.vmdl";
		base.OnStart();
	}

	protected override void AddWireComponent( GameObject go )
	{
		go.AddComponent<WireWeightScaleComponent>();
		var prop = go.GetComponent<Prop>();
		if ( prop.IsValid() )
			prop.Health = 0;
	}
}

[Icon( "bug_report" )]
[Title( "#tool.name.wiredebugger" )]
[ClassName( "wiredebugger" )]
[Group( "#tool.group.wire" )]
public class WireDebuggerTool : ToolMode
{
	public static HashSet<IWireComponent> TrackedEntities { get; } = new();

	public override string Description => "#tool.hint.wiredebugger.description";

	protected override void OnStart()
	{
		base.OnStart();
		RegisterAction( ToolInput.Primary, () => "#tool.hint.wiredebugger.add", OnAdd );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.wiredebugger.remove", OnRemove );
		RegisterAction( ToolInput.Reload, () => "#tool.hint.wiredebugger.clear", OnClear );
	}

	void OnAdd()
	{
		var select = TraceSelect();
		if ( !select.IsValid() || select.IsWorld ) return;
		if ( select.GameObject.GetComponent<IWireComponent>() is not IWireComponent wire )
			return;

		TrackedEntities.Add( wire );
		ShootEffects( select );
	}

	void OnRemove()
	{
		var select = TraceSelect();
		if ( !select.IsValid() || select.IsWorld ) return;
		if ( select.GameObject.GetComponent<IWireComponent>() is not IWireComponent wire )
			return;

		TrackedEntities.Remove( wire );
		ShootEffects( select );
	}

	void OnClear()
	{
		TrackedEntities.Clear();
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		base.DrawHud( painter, crosshair );

		TrackedEntities.RemoveWhere( ent => ent is not BaseWireComponent component || !component.IsValid() );
		if ( TrackedEntities.Count == 0 )
			return;

		var y = 80f;
		DrawDebuggerLine( painter, "Wire Debugger", 24, y, Color.Orange, 18 );
		y += 26f;

		foreach ( var ent in TrackedEntities )
		{
			var title = ent is Component c
				? (c.GameObject?.Name ?? "Wire")
				: "Wire";
			DrawDebuggerLine( painter, title, 24, y, Color.White, 15 );
			y += 18f;

			if ( ent is IWireInputComponent input )
			{
				foreach ( var name in input.GetInputNames() )
				{
					DrawDebuggerLine( painter, $"  In  {name}: {input.GetInput( name ).value}", 24, y, Color.Gray, 13 );
					y += 16f;
				}
			}

			if ( ent is IWireOutputComponent output )
			{
				foreach ( var name in output.GetOutputNames() )
				{
					DrawDebuggerLine( painter, $"  Out {name}: {output.GetOutput( name ).value}", 24, y, Color.Gray, 13 );
					y += 16f;
				}
			}

			y += 8f;
			if ( y > Screen.Height - 80f )
				break;
		}
	}

	static void DrawDebuggerLine( HudPainter painter, string text, float x, float y, Color color, float size )
	{
		var scope = new TextRendering.Scope( text, color, size );
		scope.FontName = "Consolas";
		painter.DrawText( scope, new Rect( x, y, 640, size + 4 ), TextFlag.Left );
	}
}
