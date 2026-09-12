using Sandbox.Rendering;
using Sandbox.UI;

[Icon( "cable" )]
[Title( "#tool.name.wiring" )]
[ClassName( "wiring" )]
[Group( "#tool.group.wire" )]
public class WiringTool : ToolMode
{
	GameObject _inputEnt;
	int _inputPortIndex;
	int _outputPortIndex;

	public string[] HudInputs { get; private set; } = [];
	public string[] HudOutputs { get; private set; } = [];
	public int HudInputIndex => _inputPortIndex;
	public int HudOutputIndex => _outputPortIndex;
	public bool HudInputSelected => _inputEnt.IsValid();
	public bool HudOutputSelected => _inputEnt.IsValid();

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

	protected override void OnDisabled()
	{
		base.OnDisabled();
		ResetWiring();
	}

	public override void OnControl()
	{
		base.OnControl();
		UpdateHudPorts();

		var wheel = Input.MouseWheel.y.FloorToInt();
		if ( wheel != 0 )
			CyclePorts( -wheel );
	}

	void OnPrimary()
	{
		if ( Input.Down( "drop" ) ) return;

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

	void OnCycle() => CyclePorts( Input.Down( "run" ) ? -1 : 1 );

	void CyclePorts( int delta )
	{
		if ( delta == 0 ) return;
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

	void UpdateHudPorts()
	{
		var select = TraceSelect();

		if ( _inputEnt.IsValid() && _inputEnt.GetComponent<IWireInputComponent>() is IWireInputComponent wireInput )
		{
			HudInputs = wireInput.GetInputNames( true );
			_inputPortIndex = Math.Clamp( _inputPortIndex, 0, Math.Max( 0, HudInputs.Length - 1 ) );

			if ( select.IsValid() && select.GameObject.GetComponent<IWireOutputComponent>() is IWireOutputComponent wireOutput )
			{
				HudOutputs = wireOutput.GetOutputNames( true );
				_outputPortIndex = Math.Clamp( _outputPortIndex, 0, Math.Max( 0, HudOutputs.Length - 1 ) );
			}
			else
			{
				HudOutputs = [];
			}

			return;
		}

		if ( select.IsValid() && select.GameObject.GetComponent<IWireInputComponent>() is IWireInputComponent lookInput )
		{
			HudInputs = lookInput.GetInputNames( true );
			_inputPortIndex = Math.Clamp( _inputPortIndex, 0, Math.Max( 0, HudInputs.Length - 1 ) );
		}
		else
		{
			HudInputs = [];
		}

		if ( select.IsValid() && select.GameObject.GetComponent<IWireOutputComponent>() is IWireOutputComponent lookOutput )
		{
			HudOutputs = lookOutput.GetOutputNames( true );
			_outputPortIndex = Math.Clamp( _outputPortIndex, 0, Math.Max( 0, HudOutputs.Length - 1 ) );
		}
		else
		{
			HudOutputs = [];
		}
	}

	void ResetWiring()
	{
		_inputEnt = null;
		_inputPortIndex = 0;
		_outputPortIndex = 0;
		HudInputs = [];
		HudOutputs = [];
	}

	public void RequestSpawnGate( string gateType )
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		if ( !select.IsWorld && select.GameObject.GetComponent<WireGateComponent>() is not null )
		{
			UpdateGateType( select, gateType );
			ShootEffects( select );
			return;
		}

		var gateTool = Toolgun?.GetMode<WireGateTool>();
		if ( gateTool is null ) return;

		if ( string.IsNullOrWhiteSpace( gateTool.SpawnModel ) )
			gateTool.SpawnModel = "models/wirebox/katlatze/chip_rectangle.vmdl";

		var tx = gateTool.GetPublicPlacement( select );
		gateTool.SpawnWithType( select, gateTool.SpawnModel, tx, true, gateType );
		ShootEffects( select );
	}

	[Rpc.Host]
	void UpdateGateType( SelectionPoint point, string gateType )
	{
		if ( point.GameObject.GetComponent<WireGateComponent>() is not WireGateComponent gate )
			return;

		gate.Update( string.IsNullOrWhiteSpace( gateType ) ? "Add" : gateType );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		base.DrawHud( painter, crosshair );
	}
}

[Icon( "radio_button_checked" )]
[Title( "#tool.name.wirebutton" )]
[ClassName( "wirebutton" )]
[Group( "#tool.group.wire" )]
public class WireButtonTool : WireSpawnToolMode
{
	[Property, Title( "Model" ), WireModel( "button" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Model" ), WireModel( "gps", "controller" )]
	public override string SpawnModel { get; set; }

	public override string Description => "#tool.hint.wiregps.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/seal_enthusiast/gps/gps.vmdl";
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
	[Property, Title( "Model" ), WireModel( "weight", "controller" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Model" ), WireModel( "speedometer", "controller" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Model" ), WireModel( "gyroscope", "controller" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Model" ), WireModel( "ranger", "controller" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Model" ), WireModel( "ranger", "forcer", "controller" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Model" ), WireModel( "gate", "controller" )]
	public override string SpawnModel { get; set; }

	[Property, Sync, Title( "Gate Type" )]
	public string GateType { get; set; } = "Add";

	public override string Description => "#tool.hint.wiregate.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/katlatze/chip_rectangle.vmdl";
		base.OnStart();
		RegisterAction( ToolInput.Reload, () => "#tool.hint.wiregate.update", OnUpdateExisting );
	}

	protected override void AddWireComponent( GameObject go )
	{
		var gate = go.AddComponent<WireGateComponent>();
		gate.GateType = ResolveGateType();
		gate.WireInitialize();
	}

	public void SetGateType( string gateType )
	{
		if ( string.IsNullOrWhiteSpace( gateType ) ) return;
		if ( !IsKnownGateType( gateType ) ) return;
		GateType = gateType;
	}

	public Transform GetPublicPlacement( SelectionPoint select ) => GetPlacement( select );

	[Rpc.Host]
	public void SpawnWithType( SelectionPoint point, string modelPath, Transform tx, bool weld, string gateType )
	{
		if ( IsKnownGateType( gateType ) )
			GateType = gateType;
		else
			GateType = "Add";

		SpawnInternal( point, modelPath, tx, weld );
	}

	string ResolveGateType()
	{
		return IsKnownGateType( GateType ) ? GateType : "Add";
	}

	static bool IsKnownGateType( string gateType )
	{
		if ( string.IsNullOrWhiteSpace( gateType ) ) return false;
		return WireGateComponent.GetGates().Values.Any( list => list.Contains( gateType ) );
	}

	void OnUpdateExisting()
	{
		var select = TraceSelect();
		if ( !select.IsValid() || select.IsWorld ) return;
		if ( select.GameObject.GetComponent<WireGateComponent>() is not WireGateComponent gate )
			return;

		UpdateExistingGate( select, ResolveGateType() );
		ShootEffects( select );
	}

	[Rpc.Host]
	void UpdateExistingGate( SelectionPoint point, string gateType )
	{
		if ( point.GameObject.GetComponent<WireGateComponent>() is not WireGateComponent gate )
			return;

		gate.Update( IsKnownGateType( gateType ) ? gateType : "Add" );
	}
}

[Icon( "monitor" )]
[Title( "#tool.name.wiredigitalscreen" )]
[ClassName( "wiredigitalscreen" )]
[Group( "#tool.group.wire" )]
public class WireDigitalScreenTool : WireSpawnToolMode
{
	[Property, Title( "Model" ), WireModel( "screen", "controller" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Screen Model" ), WireModel( "screen", "controller" )]
	public override string SpawnModel { get; set; }

	[Property, Title( "Camera Model" ), WireModel( "camera", "controller" )]
	public string CameraModel { get; set; } = "camera/camera.vmdl";

	protected override bool RegisterNoWeldSecondary => false;
	public override string Description => "#tool.hint.wirecamerascreen.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/television/flatscreen_tv.vmdl";
		if ( string.IsNullOrWhiteSpace( CameraModel ) )
			CameraModel = "camera/camera.vmdl";
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

		var tx = GetPlacement( select );
		_ = SpawnCameraResolved( select, CameraModel, tx );
	}

	async Task SpawnCameraResolved( SelectionPoint select, string modelPath, Transform tx )
	{
		var model = await ResolveSpawnModel( modelPath )
			?? await ResolveSpawnModel( "camera/camera.vmdl" )
			?? await ResolveSpawnModel( "smlp.camera" )
			?? await ResolveSpawnModel( "models/wirebox/katlatze/apc.vmdl" );
		if ( model is null ) return;

		SpawnCamera( select, modelPath, tx );
		ShootEffects( select );
	}

	[Rpc.Host]
	void SpawnCamera( SelectionPoint point, string modelPath, Transform tx )
	{
		_ = SpawnCameraAsync( point, modelPath, tx );
	}

	async Task SpawnCameraAsync( SelectionPoint point, string modelPath, Transform tx )
	{
		if ( !CanUseToolOn( point ) ) return;
		if ( string.IsNullOrWhiteSpace( modelPath ) ) return;

		var model = await ResolveSpawnModel( modelPath )
			?? await ResolveSpawnModel( "camera/camera.vmdl" )
			?? await ResolveSpawnModel( "smlp.camera" )
			?? await ResolveSpawnModel( "models/wirebox/katlatze/apc.vmdl" );
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

		go.AddComponent<WireCameraComponent>();

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
	[Property, Title( "Model" ), WireModel( "keyboard", "controller", "button" )]
	public override string SpawnModel { get; set; }

	public override string Description => "#tool.hint.wirekeyboard.description";

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( SpawnModel ) )
			SpawnModel = "models/wirebox/seal_enthusiast/keyboard/keyboard.vmdl";
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
	[Property, Title( "Model" ), WireModel( "lightbridge", "controller" )]
	public override string SpawnModel { get; set; }

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
	[Property, Title( "Model" ), WireModel( "weightscale" )]
	public override string SpawnModel { get; set; }

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
	}
}
