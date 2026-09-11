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
