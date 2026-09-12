using Sandbox.UI;
using Sandbox.UI.Construct;

/// <summary>
/// Willox-style keypad: baked face UI on a dark housing, dynamic screen text,
/// fading-door key pulses, and wire outputs. Matches GMod workshop 108424005 visuals.
/// </summary>
[Library( "ent_keypad", Title = "Keypad" )]
public sealed class KeypadComponent : BaseWireOutputComponent, Component.IPressable
{
	public const float UseDistance = 120f;
	const float CommandCooldown = 0.05f;

	// Willox screen rect (normalized face coords from keypad_face.png).
	const float ScreenX = 0.075f;
	const float ScreenY = 0.04f;
	const float ScreenW = 0.85f;
	const float ScreenH = 0.25f;

	public enum StatusType : int
	{
		None = 0,
		Granted = 1,
		Denied = 2
	}

	public enum CommandType : int
	{
		Enter = 0,
		Accept = 1,
		Abort = 2
	}

	enum FaceButton
	{
		None,
		Abort,
		Accept,
		Digit1,
		Digit2,
		Digit3,
		Digit4,
		Digit5,
		Digit6,
		Digit7,
		Digit8,
		Digit9
	}

	readonly struct FaceElement
	{
		public readonly float X;
		public readonly float Y;
		public readonly float W;
		public readonly float H;
		public readonly FaceButton Button;

		public FaceElement( float x, float y, float w, float h, FaceButton button )
		{
			X = x;
			Y = y;
			W = w;
			H = h;
			Button = button;
		}

		public bool Contains( float u, float v ) =>
			u >= X && u <= X + W && v >= Y && v <= Y + H;
	}

	/// <summary>Baked face button layout — matches keypad_face.png / build_keypad_fbx.py.</summary>
	static readonly FaceElement[] HitElements =
	[
		new( 0.075f, 0.32f, 0.455f, 0.125f, FaceButton.Abort ),
		new( 0.57f, 0.32f, 0.355f, 0.125f, FaceButton.Accept ),
		new( 0.075f, 0.475f, 0.25f, 0.13f, FaceButton.Digit1 ),
		new( 0.375f, 0.475f, 0.25f, 0.13f, FaceButton.Digit2 ),
		new( 0.675f, 0.475f, 0.25f, 0.13f, FaceButton.Digit3 ),
		new( 0.075f, 0.6416667f, 0.25f, 0.13f, FaceButton.Digit4 ),
		new( 0.375f, 0.6416667f, 0.25f, 0.13f, FaceButton.Digit5 ),
		new( 0.675f, 0.6416667f, 0.25f, 0.13f, FaceButton.Digit6 ),
		new( 0.075f, 0.8083333f, 0.25f, 0.13f, FaceButton.Digit7 ),
		new( 0.375f, 0.8083333f, 0.25f, 0.13f, FaceButton.Digit8 ),
		new( 0.675f, 0.8083333f, 0.25f, 0.13f, FaceButton.Digit9 ),
	];

	/// <summary>Server-only password. Never synced.</summary>
	string _password = "1337";
	string _entry = "";

	TimeSince _timeSinceCommand = CommandCooldown;
	TimeUntil _resetAt;
	TimeUntil _holdEndsAt;
	bool _holdActive;
	readonly List<FadingDoor> _heldDoors = new();

	[Sync] public string DisplayText { get; private set; } = "";
	[Sync] public StatusType Status { get; private set; } = StatusType.None;
	[Sync] public bool Secure { get; private set; } = false;
	[Sync] public string AccessGrantedKey { get; private set; } = "5";
	[Sync] public string AccessDeniedKey { get; private set; } = "";
	[Sync] public float LengthGranted { get; private set; } = 4f;
	[Sync] public float LengthDenied { get; private set; } = 0.1f;
	[Sync] bool StatusLeet { get; set; }

	// Client-local screen overlay only — buttons are baked into the model texture.
	Sandbox.WorldPanel _worldPanelComponent;
	GameObject _mountPoint;
	Sandbox.UI.WorldPanel _worldPanel;
	Label _displayLabel;
	Label _statusLine1;
	Label _statusLine2;
	bool _screenBuilt;

	public static bool IsValidPassword( string password )
	{
		if ( string.IsNullOrWhiteSpace( password ) )
			return false;
		if ( password.Length > 4 )
			return false;

		foreach ( var c in password )
		{
			if ( c < '1' || c > '9' )
				return false;
		}

		return true;
	}

	public void Configure( string password, bool secure, string grantedKey, string deniedKey, float lengthGranted, float lengthDenied )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsValidPassword( password ) )
			_password = password;

		Secure = secure;
		AccessGrantedKey = grantedKey ?? "";
		AccessDeniedKey = deniedKey ?? "";
		LengthGranted = Math.Clamp( lengthGranted, 0.05f, 30f );
		LengthDenied = Math.Clamp( lengthDenied, 0.05f, 30f );
		ResetEntry();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		EnsureClientPanel();
	}

	protected override void OnDisabled()
	{
		DestroyClientPanel();
		base.OnDisabled();
	}

	protected override void OnDestroy()
	{
		DestroyClientPanel();
		base.OnDestroy();
	}

	void EnsureClientPanel()
	{
		if ( _mountPoint.IsValid() )
			return;

		_mountPoint = new GameObject( true, "keypad_screen" )
		{
			Parent = GameObject,
			LocalPosition = Vector3.Zero,
			LocalRotation = Rotation.Identity
		};
		_worldPanelComponent = _mountPoint.AddComponent<Sandbox.WorldPanel>();
	}

	void DestroyClientPanel()
	{
		_displayLabel = null;
		_statusLine1 = null;
		_statusLine2 = null;
		_worldPanel = null;
		_worldPanelComponent = null;
		_screenBuilt = false;

		if ( _mountPoint.IsValid() )
			_mountPoint.Destroy();

		_mountPoint = null;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		UpdateScreenOverlay();
		PollKeyboardInput();
		TickHostTimers();
	}

	void TickHostTimers()
	{
		if ( !Networking.IsHost )
			return;

		if ( _holdActive && _holdEndsAt )
		{
			foreach ( var door in _heldDoors )
			{
				if ( door.IsValid() )
					door.ReleaseFromKeypad();
			}

			_heldDoors.Clear();
			this.WireTriggerOutput( "AccessGranted", false );
			this.WireTriggerOutput( "AccessDenied", false );
			_holdActive = false;
		}

		if ( Status != StatusType.None && _resetAt )
			ResetEntry();
	}

	void PollKeyboardInput()
	{
		var player = Player.FindLocalPlayer();
		if ( !player.IsValid() || !player.IsLocalPlayer )
			return;

		if ( !IsPlayerLookingAtKeypad( player, out var hit ) )
			return;

		var menu = Game.ActiveScene.Get<SpawnMenuHost>();
		if ( menu?.Panel?.HasClass( "open" ) == true )
			return;

		if ( KeyBindControl.IsCapturing )
			return;

		// Willox-style: E / use aims at a face button and presses it.
		if ( Input.Pressed( "use" ) && TryPressFaceButton( hit ) )
		{
			Input.Clear( "use" );
			return;
		}

		for ( var digit = 1; digit <= 9; digit++ )
		{
			if ( Input.Keyboard.Pressed( digit.ToString() ) )
			{
				RequestCommand( CommandType.Enter, digit );
				return;
			}
		}

		if ( Input.Keyboard.Pressed( "enter" ) || Input.Keyboard.Pressed( "pad_enter" ) )
		{
			RequestCommand( CommandType.Accept );
			return;
		}

		if ( Input.Keyboard.Pressed( "backspace" ) || Input.Keyboard.Pressed( "delete" ) )
			RequestCommand( CommandType.Abort );
	}

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( Status == StatusType.Granted )
			return new IPressable.Tooltip( "Use Keypad", "dialpad", "ACCESS GRANTED" );
		if ( Status == StatusType.Denied )
			return new IPressable.Tooltip( "Use Keypad", "dialpad", "ACCESS DENIED" );

		var player = Player.FindLocalPlayer();
		if ( player.IsValid() && IsPlayerLookingAtKeypad( player, out var hit ) )
		{
			var button = ResolveFaceButton( hit );
			var hover = DescribeFaceButton( button );
			if ( hover is not null )
				return new IPressable.Tooltip( "Press", "dialpad", hover );
		}

		var description = string.IsNullOrEmpty( DisplayText ) ? "Aim at a button" : DisplayText;
		return new IPressable.Tooltip( "Use Keypad", "dialpad", description );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		if ( Status != StatusType.None )
			return false;

		var player = Player.FindForConnection( e.Source.Network.Owner ) ?? Player.FindLocalPlayer();
		if ( !player.IsValid() )
			return false;

		return IsPlayerLookingAtKeypad( player, out var hit ) && ResolveFaceButton( hit ) != FaceButton.None;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		var player = Player.FindForConnection( e.Source.Network.Owner ) ?? Player.FindLocalPlayer();
		if ( !player.IsValid() )
			return false;

		if ( !IsPlayerLookingAtKeypad( player, out var hit ) )
			return false;

		return TryPressFaceButton( hit );
	}

	bool TryPressFaceButton( Vector3 hit )
	{
		var button = ResolveFaceButton( hit );
		if ( button == FaceButton.None )
			return false;

		switch ( button )
		{
			case FaceButton.Abort:
				RequestCommand( CommandType.Abort );
				break;
			case FaceButton.Accept:
				RequestCommand( CommandType.Accept );
				break;
			default:
				var digit = (int)button - (int)FaceButton.Digit1 + 1;
				RequestCommand( CommandType.Enter, digit );
				break;
		}

		return true;
	}

	static string DescribeFaceButton( FaceButton button ) => button switch
	{
		FaceButton.Abort => "ABORT",
		FaceButton.Accept => "OK",
		FaceButton.Digit1 => "1",
		FaceButton.Digit2 => "2",
		FaceButton.Digit3 => "3",
		FaceButton.Digit4 => "4",
		FaceButton.Digit5 => "5",
		FaceButton.Digit6 => "6",
		FaceButton.Digit7 => "7",
		FaceButton.Digit8 => "8",
		FaceButton.Digit9 => "9",
		_ => null
	};

	[Rpc.Host]
	void RequestCommand( CommandType command, int digit = 0 )
	{
		var caller = Rpc.Caller;
		if ( caller is null )
			return;

		var player = Player.FindForConnection( caller );
		if ( !player.IsValid() )
			return;

		if ( player.EyeTransform.Position.Distance( WorldPosition ) > UseDistance )
			return;

		if ( Status != StatusType.None )
			return;

		if ( _timeSinceCommand < CommandCooldown )
			return;

		_timeSinceCommand = 0;

		switch ( command )
		{
			case CommandType.Enter:
				if ( digit < 1 || digit > 9 )
					return;
				if ( _entry.Length >= 4 )
					return;

				_entry += digit.ToString();
				RefreshDisplayFromEntry();
				Sound.Play( "flashlight-on", WorldPosition );
				break;

			case CommandType.Abort:
				_entry = "";
				RefreshDisplayFromEntry();
				Sound.Play( "flashlight-off", WorldPosition );
				break;

			case CommandType.Accept:
				Process( string.Equals( _entry, _password, StringComparison.Ordinal ) );
				break;
		}
	}

	void Process( bool granted )
	{
		if ( !Networking.IsHost )
			return;

		var leet = string.Equals( _entry, "1337", StringComparison.Ordinal );
		_entry = "";
		Status = granted ? StatusType.Granted : StatusType.Denied;
		StatusLeet = leet;
		DisplayText = granted ? "ACCESS GRANTED" : "ACCESS DENIED";

		var length = granted ? LengthGranted : LengthDenied;
		var key = granted ? AccessGrantedKey : AccessDeniedKey;

		this.WireTriggerOutput( granted ? "AccessGranted" : "AccessDenied", true );
		this.WireTriggerOutput( granted ? "AccessDenied" : "AccessGranted", false );

		ActivateLinkedDoors( key );

		_holdEndsAt = length;
		_holdActive = true;
		_resetAt = MathF.Max( length + 0.25f, 2f );

		Sound.Play( granted ? "flashlight-on" : "flashlight-off", WorldPosition );
	}

	void ActivateLinkedDoors( string key )
	{
		_heldDoors.Clear();

		if ( string.IsNullOrWhiteSpace( key ) || Game.ActiveScene is null )
			return;

		if ( !ObjectAccess.TryGetOwnable( GameObject, out var keypadOwnable ) || keypadOwnable.Owner is null )
			return;

		var owner = keypadOwnable.Owner;

		foreach ( var door in Game.ActiveScene.GetAllComponents<FadingDoor>() )
		{
			if ( !door.IsValid() )
				continue;

			if ( !string.Equals( door.FadeKey, key, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( !ObjectAccess.TryGetOwnable( door.GameObject, out var doorOwnable ) || doorOwnable.Owner != owner )
				continue;

			door.ActivateFromKeypad();

			if ( !door.Toggle )
				_heldDoors.Add( door );
		}
	}

	void ResetEntry()
	{
		_entry = "";
		Status = StatusType.None;
		StatusLeet = false;
		RefreshDisplayFromEntry();
	}

	void RefreshDisplayFromEntry()
	{
		if ( Status != StatusType.None )
			return;

		// Green screen always shows typed digits in clear form.
		DisplayText = _entry ?? "";
	}

	bool IsPlayerLookingAtKeypad( Player player, out Vector3 hitPosition )
	{
		hitPosition = default;
		if ( !player.IsValid() || Game.ActiveScene is null )
			return false;

		var eye = player.EyeTransform;
		var tr = Game.ActiveScene.Trace.Ray( eye.Position, eye.Position + eye.Forward * UseDistance )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "player", "trigger" )
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() )
			return false;

		var hitKeypad = tr.GameObject.GetComponent<KeypadComponent>()
			?? tr.GameObject.GetComponentInParent<KeypadComponent>()
			?? tr.GameObject.GetComponentInChildren<KeypadComponent>();

		if ( hitKeypad != this )
			return false;

		hitPosition = tr.HitPosition;
		return true;
	}

	FaceButton ResolveFaceButton( Vector3 worldHit )
	{
		if ( !TryGetFaceUv( worldHit, out var u, out var v ) )
			return FaceButton.None;

		foreach ( var element in HitElements )
		{
			if ( element.Contains( u, v ) )
				return element.Button;
		}

		return FaceButton.None;
	}

	/// <summary>
	/// Map a world hit to baked-face UVs as the player sees them: u=0 left, v=0 top.
	/// Uses the wall-facing plane and world-up so button hits track the texture, not model axes.
	/// </summary>
	bool TryGetFaceUv( Vector3 worldHit, out float u, out float v )
	{
		u = 0;
		v = 0;

		if ( !TryGetFaceBasis( out var faceCenter, out var faceRight, out var faceUp, out var halfW, out var halfH ) )
			return false;

		var delta = worldHit - faceCenter;
		var x = Vector3.Dot( delta, faceRight );
		var y = Vector3.Dot( delta, faceUp );

		u = Math.Clamp( (x + halfW) / (2f * halfW), 0f, 1f );
		v = Math.Clamp( (halfH - y) / (2f * halfH), 0f, 1f );
		return true;
	}

	bool TryGetFaceBasis( out Vector3 faceCenter, out Vector3 faceRight, out Vector3 faceUp, out float halfW, out float halfH )
	{
		faceCenter = default;
		faceRight = default;
		faceUp = default;
		halfW = 0;
		halfH = 0;

		var renderer = GetComponent<ModelRenderer>() ?? GetComponentInChildren<ModelRenderer>();
		if ( !renderer.IsValid() || !renderer.Model.IsValid() )
			return false;

		var bounds = renderer.Model.Bounds;
		var size = bounds.Size;
		if ( size.x < 0.001f || size.y < 0.001f || size.z < 0.001f )
			return false;

		// Placement aims Forward into the wall — face normal toward the player is -Forward.
		var faceNormal = -WorldRotation.Forward;

		faceRight = Vector3.Cross( Vector3.Up, faceNormal );
		if ( faceRight.LengthSquared < 0.0001f )
			faceRight = Vector3.Cross( Vector3.Forward, faceNormal );
		faceRight = faceRight.Normal;
		faceUp = Vector3.Cross( faceNormal, faceRight ).Normal;

		// Push bounds center onto the outward face along the dominant local normal.
		var localNormal = WorldRotation.Inverse * faceNormal;
		var centerLocal = bounds.Center;
		var absX = MathF.Abs( localNormal.x );
		var absY = MathF.Abs( localNormal.y );
		var absZ = MathF.Abs( localNormal.z );
		if ( absX >= absY && absX >= absZ )
			centerLocal.x = localNormal.x >= 0f ? bounds.Maxs.x : bounds.Mins.x;
		else if ( absY >= absX && absY >= absZ )
			centerLocal.y = localNormal.y >= 0f ? bounds.Maxs.y : bounds.Mins.y;
		else
			centerLocal.z = localNormal.z >= 0f ? bounds.Maxs.z : bounds.Mins.z;

		faceCenter = WorldTransform.PointToWorld( centerLocal );

		var localRight = WorldRotation.Inverse * faceRight;
		var localUp = WorldRotation.Inverse * faceUp;
		halfW = 0.5f * (MathF.Abs( localRight.x ) * size.x + MathF.Abs( localRight.y ) * size.y + MathF.Abs( localRight.z ) * size.z);
		halfH = 0.5f * (MathF.Abs( localUp.x ) * size.x + MathF.Abs( localUp.y ) * size.y + MathF.Abs( localUp.z ) * size.z);

		return halfW > 0.01f && halfH > 0.01f;
	}

	void UpdateScreenOverlay()
	{
		EnsureClientPanel();

		if ( !_worldPanelComponent.IsValid() )
			return;

		_worldPanel = _worldPanelComponent.GetPanel() as Sandbox.UI.WorldPanel;
		if ( !_worldPanel.IsValid() )
			return;

		if ( !_screenBuilt )
			BuildScreenOverlay();

		if ( !_screenBuilt )
			return;

		ApplyScreenTransform();
		RefreshScreenLabels();
	}

	void RefreshScreenLabels()
	{
		if ( _displayLabel is null || _statusLine1 is null || _statusLine2 is null )
			return;

		ScaleScreenFonts();

		if ( Status == StatusType.None )
		{
			_displayLabel.Style.Opacity = 1;
			_statusLine1.Style.Opacity = 0;
			_statusLine2.Style.Opacity = 0;
			_displayLabel.Text = DisplayText ?? "";
			_displayLabel.Style.FontColor = Color.White;
			return;
		}

		_displayLabel.Style.Opacity = 0;
		_statusLine1.Style.Opacity = 1;
		_statusLine2.Style.Opacity = 1;

		var leet = StatusLeet;
		if ( Status == StatusType.Granted )
		{
			_statusLine1.Text = leet ? "ACC355" : "ACCESS";
			_statusLine2.Text = leet ? "GRAN73D" : "GRANTED";
			_statusLine1.Style.FontColor = Color.White;
			_statusLine2.Style.FontColor = new Color( 0.35f, 1f, 0.35f );
		}
		else
		{
			_statusLine1.Text = leet ? "ACC355" : "ACCESS";
			_statusLine2.Text = leet ? "D3N13D" : "DENIED";
			_statusLine1.Style.FontColor = Color.White;
			_statusLine2.Style.FontColor = new Color( 1f, 0.35f, 0.35f );
		}
	}

	void ScaleScreenFonts()
	{
		if ( !_worldPanelComponent.IsValid() || _displayLabel is null )
			return;

		var h = _worldPanelComponent.PanelSize.y;
		if ( h < 1f )
			return;

		// Fit entry / status inside the green screen panel pixels.
		_displayLabel.Style.FontSize = Length.Pixels( MathF.Max( 14f, h * 0.5f ) );
		_statusLine1.Style.FontSize = Length.Pixels( MathF.Max( 10f, h * 0.26f ) );
		_statusLine2.Style.FontSize = Length.Pixels( MathF.Max( 10f, h * 0.26f ) );
	}

	void BuildScreenOverlay()
	{
		var renderer = GetComponent<ModelRenderer>() ?? GetComponentInChildren<ModelRenderer>();
		if ( !renderer.IsValid() || !renderer.Model.IsValid() )
			return;

		_worldPanel.DeleteChildren( true );
		_worldPanel.Style.Width = Length.Percent( 100 );
		_worldPanel.Style.Height = Length.Percent( 100 );
		_worldPanel.Style.BackgroundColor = Color.Transparent;
		_worldPanel.Style.JustifyContent = Justify.Center;
		_worldPanel.Style.AlignItems = Align.Center;
		_worldPanel.Style.FlexDirection = FlexDirection.Column;
		_worldPanel.Style.Overflow = OverflowMode.Hidden;
		_worldPanel.Style.Padding = Length.Pixels( 2 );

		_displayLabel = _worldPanel.Add.Label( "", "keypad-entry" );
		_displayLabel.Style.Width = Length.Percent( 100 );
		_displayLabel.Style.TextAlign = TextAlign.Center;
		_displayLabel.Style.FontSize = Length.Pixels( 24 );
		_displayLabel.Style.FontWeight = 800;
		_displayLabel.Style.FontColor = Color.White;
		_displayLabel.Style.Overflow = OverflowMode.Hidden;

		_statusLine1 = _worldPanel.Add.Label( "", "keypad-status" );
		_statusLine1.Style.Width = Length.Percent( 100 );
		_statusLine1.Style.TextAlign = TextAlign.Center;
		_statusLine1.Style.FontSize = Length.Pixels( 12 );
		_statusLine1.Style.FontWeight = 800;
		_statusLine1.Style.Opacity = 0;
		_statusLine1.Style.Overflow = OverflowMode.Hidden;

		_statusLine2 = _worldPanel.Add.Label( "", "keypad-status" );
		_statusLine2.Style.Width = Length.Percent( 100 );
		_statusLine2.Style.TextAlign = TextAlign.Center;
		_statusLine2.Style.FontSize = Length.Pixels( 12 );
		_statusLine2.Style.FontWeight = 800;
		_statusLine2.Style.Opacity = 0;
		_statusLine2.Style.Overflow = OverflowMode.Hidden;

		_screenBuilt = true;
		ApplyScreenTransform();
		ScaleScreenFonts();
	}

	/// <summary>
	/// Place a transparent text overlay on the green screen rect.
	/// WorldPanel renders on its local XY facing along Forward — aim Forward at the player.
	/// </summary>
	void ApplyScreenTransform()
	{
		if ( !_mountPoint.IsValid() || !_worldPanelComponent.IsValid() )
			return;

		if ( !TryGetFaceBasis( out var faceCenter, out var faceRight, out var faceUp, out var halfW, out var halfH ) )
			return;

		var faceNormal = -WorldRotation.Forward;
		var cu = ScreenX + ScreenW * 0.5f;
		var cv = ScreenY + ScreenH * 0.5f;

		// u=0 left, v=0 top on the visual face.
		var x = MathX.Lerp( -halfW, halfW, cu );
		var y = MathX.Lerp( halfH, -halfH, cv );
		var worldPos = faceCenter + faceRight * x + faceUp * y + faceNormal * 0.05f;
		_mountPoint.WorldPosition = worldPos;
		_mountPoint.WorldRotation = Rotation.LookAt( faceNormal, faceUp );

		var screenWorldW = halfW * 2f * ScreenW;
		var screenWorldH = halfH * 2f * ScreenH;
		_worldPanelComponent.PanelSize = new Vector2( screenWorldW, screenWorldH ) / Sandbox.UI.WorldPanel.ScreenToWorldScale;
	}

	public override PortType[] WireGetOutputs()
	{
		return
		[
			PortType.Bool( "AccessGranted" ),
			PortType.Bool( "AccessDenied" )
		];
	}
}
