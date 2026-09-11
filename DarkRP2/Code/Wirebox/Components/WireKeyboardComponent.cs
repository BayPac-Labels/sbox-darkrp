[Library( "ent_wirekeyboard", Title = "Wire Keyboard" )]
public partial class WireKeyboardComponent : BaseWireOutputComponent, Component.IPressable
{
	static readonly Dictionary<string, string> InputButtons = new()
	{
		["Forward"] = "Forward",
		["Backward"] = "Backward",
		["Left"] = "Left",
		["Right"] = "Right",
		["Attack1"] = "Attack1",
		["Attack2"] = "Attack2",
		["Reload"] = "Reload",
		["Drop"] = "Drop",
		["Jump"] = "Jump",
		["Run"] = "Run",
		["Walk"] = "Walk",
		["Duck"] = "Duck",
		["Score"] = "Score",
		["Menu"] = "Menu",
		["Flashlight"] = "Flashlight",
		["View"] = "View",
		["Voice"] = "Voice",
		["Slot1"] = "Slot1",
		["Slot2"] = "Slot2",
		["Slot3"] = "Slot3",
		["Slot4"] = "Slot4",
		["Slot5"] = "Slot5",
		["Slot6"] = "Slot6",
		["Slot7"] = "Slot7",
		["Slot8"] = "Slot8",
		["Slot9"] = "Slot9",
	};

	[Sync]
	Guid ActivePlayerId { get; set; }

	Player ActivePlayer => Player.For( ActivePlayerId );

	bool IPressable.CanPress( IPressable.Event e ) => true;

	bool IPressable.Press( IPressable.Event e )
	{
		if ( !ActivePlayer.IsValid() )
		{
			var player = Player.FindForConnection( e.Source.Network.Owner );
			if ( !player.IsValid() )
				return false;

			SetKeyboardActive( player.PlayerId, true );
		}
		else
		{
			SetKeyboardActive( ActivePlayerId, false );
		}

		return true;
	}

	[Rpc.Broadcast]
	void SetKeyboardActive( Guid playerId, bool active )
	{
		var player = Player.For( playerId );
		if ( active )
		{
			ActivePlayerId = playerId;
			if ( player.IsValid() && player.Controller.IsValid() )
				player.Controller.UseInputControls = false;

			if ( player.IsValid() && player.IsLocalPlayer )
				player.GetComponent<PlayerInventory>()?.SwitchWeapon( null, true );

			this.WireTriggerOutput( "Active", true );
			return;
		}

		if ( player.IsValid() && player.Controller.IsValid() )
			player.Controller.UseInputControls = true;

		this.WireTriggerOutput( "Active", false );
		ActivePlayerId = Guid.Empty;
	}

	protected override void OnDisabled()
	{
		if ( ActivePlayerId != Guid.Empty )
			SetKeyboardActive( ActivePlayerId, false );
	}

	protected override void OnUpdate()
	{
		var player = ActivePlayer;
		if ( !player.IsValid() || !player.IsLocalPlayer )
			return;

		if ( Input.EscapePressed )
		{
			Input.EscapePressed = false;
			RequestDisable();
			return;
		}

		var eyes = player.EyeTransform.Rotation;
		var move = Input.AnalogMove.ClampLength( 1f );

		foreach ( var name in InputButtons.Keys )
		{
			var inputButton = InputButtons[name];
			var newState = Input.Down( inputButton );
			if ( newState != (bool)this.GetOutput( name ).value )
				this.WireTriggerOutput( name, newState );
		}

		this.WireTriggerOutput( "Eyes", eyes.Forward );
		this.WireTriggerOutput( "Move", move.WithZ( (Input.Down( "Duck" ) ? 1f : 0f) - (Input.Down( "Jump" ) ? 1f : 0f) ) );
		this.WireTriggerOutput( "MouseWheel", Input.MouseWheel.y );
	}

	[Rpc.Host]
	void RequestDisable()
	{
		if ( ActivePlayerId == Guid.Empty )
			return;

		SetKeyboardActive( ActivePlayerId, false );
	}

	public override PortType[] WireGetOutputs()
	{
		return new PortType[]
			{
				PortType.Bool( "Active" ),
				PortType.Vector3( "Move" ),
				PortType.Vector3( "Eyes" ),
				PortType.Float( "MouseWheel" ),
			}
			.Concat( InputButtons.Keys.Select( x => PortType.Bool( x ) ) )
			.ToArray();
	}
}
