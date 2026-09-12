/// <summary>
/// Makes a prop fade (non-solid + translucent) when activated by key or wire.
/// </summary>
public sealed class FadingDoor : BaseWireInputOutputComponent
{
	[Property, Sync, ClientEditable, KeyBind, Title( "Fade Key" )]
	public string FadeKey { get; set; } = "5";

	[Property, Sync, ClientEditable]
	public bool Toggle { get; set; } = true;

	[Property, Sync, ClientEditable, Title( "Start Faded" )]
	public bool StartFaded { get; set; }

	[Property, Sync, ClientEditable, Title( "No Effect" )]
	public bool NoEffect { get; set; }

	[Sync]
	public bool IsFaded { get; private set; }

	bool _appliedVisual;
	bool _wireWasOn;
	bool _reportedDown;
	bool _keyWasDown;
	bool _motionWasEnabled = true;
	readonly Dictionary<Collider, bool> _colliderWasEnabled = new();
	readonly Dictionary<ModelRenderer, Color> _originalTints = new();

	static readonly Color FadedTint = new( 0.45f, 0.75f, 1f, 0.2f );

	protected override void OnStart()
	{
		base.OnStart();

		if ( Networking.IsHost && StartFaded && !IsFaded )
			SetFaded( true );
	}

	protected override void OnDestroy()
	{
		if ( IsFaded )
			RestoreSolidState();

		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		ApplyVisualState();
		PollFadeKey();
	}

	public override void WireInitialize()
	{
		this.RegisterInputHandler( "Fade", ( float value ) =>
		{
			var on = value > 0.01f;

			if ( on && !_wireWasOn )
			{
				_wireWasOn = true;
				ToggleFade();
			}
			else if ( !on && _wireWasOn )
			{
				_wireWasOn = false;
				if ( !Toggle )
					SetFaded( false );
			}
		} );
	}

	public override PortType[] WireGetOutputs()
	{
		return [PortType.Bool( "FadeActive" )];
	}

	bool IsLocalOwner()
	{
		if ( !ObjectAccess.TryGetOwnable( GameObject, out var ownable ) || ownable.Owner is null )
			return false;

		return ownable.Owner == Connection.Local;
	}

	void PollFadeKey()
	{
		if ( string.IsNullOrWhiteSpace( FadeKey ) )
			return;

		if ( !IsLocalOwner() )
			return;

		// Don't fire while binding a key or while the spawn/inspect menu is open
		if ( Sandbox.UI.KeyBindControl.IsCapturing )
			return;

		var menu = Game.ActiveScene.Get<SpawnMenuHost>();
		if ( menu?.Panel?.HasClass( "open" ) == true )
			return;

		var pressed = Input.Keyboard.Pressed( FadeKey );
		var down = Input.Keyboard.Down( FadeKey );
		var released = Input.Keyboard.Released( FadeKey );

		if ( !pressed && !released && down == _reportedDown )
			return;

		_reportedDown = down;

		if ( Networking.IsHost )
			HandleKeyInput( pressed, down, released );
		else
			ReportFadeKey( pressed, down, released );
	}

	[Rpc.Host]
	void ReportFadeKey( bool pressed, bool down, bool released )
	{
		if ( !ObjectAccess.TryGetOwnable( GameObject, out var ownable ) || ownable.Owner != Rpc.Caller )
			return;

		HandleKeyInput( pressed, down, released );
	}

	void HandleKeyInput( bool pressed, bool down, bool released )
	{
		if ( Toggle )
		{
			if ( pressed )
				ToggleFade();
		}
		else
		{
			if ( pressed || (down && !_keyWasDown) )
				SetFaded( true );
			else if ( released || (!down && _keyWasDown) )
				SetFaded( false );
		}

		_keyWasDown = down;
	}

	public void ToggleFade()
	{
		SetFaded( !IsFaded );
	}

	/// <summary>
	/// Host-only activation from a keypad (or similar). Toggle doors flip once;
	/// hold doors fade open and stay open until <see cref="ReleaseFromKeypad"/>.
	/// </summary>
	public void ActivateFromKeypad()
	{
		if ( !Networking.IsHost )
			return;

		if ( Toggle )
			ToggleFade();
		else
			SetFaded( true );
	}

	/// <summary>
	/// Host-only release for non-toggle fading doors after a keypad hold length.
	/// </summary>
	public void ReleaseFromKeypad()
	{
		if ( !Networking.IsHost )
			return;

		if ( !Toggle )
			SetFaded( false );
	}

	public void SetFaded( bool faded )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsFaded == faded )
			return;

		IsFaded = faded;

		if ( faded )
			ApplyFadedPhysics();
		else
			RestoreSolidState();

		this.WireTriggerOutput( "FadeActive", faded );
		ApplyVisualState( force: true );
	}

	void ApplyFadedPhysics()
	{
		_colliderWasEnabled.Clear();

		foreach ( var collider in GameObject.GetComponentsInChildren<Collider>( true ) )
		{
			if ( !collider.IsValid() )
				continue;

			_colliderWasEnabled[collider] = collider.Enabled;
			collider.Enabled = false;
		}

		var rb = GameObject.GetComponent<Rigidbody>();
		if ( rb.IsValid() )
		{
			_motionWasEnabled = rb.MotionEnabled;
			rb.MotionEnabled = false;
		}
	}

	void RestoreSolidState()
	{
		foreach ( var (collider, wasEnabled) in _colliderWasEnabled )
		{
			if ( collider.IsValid() )
				collider.Enabled = wasEnabled;
		}

		_colliderWasEnabled.Clear();

		var rb = GameObject.GetComponent<Rigidbody>();
		if ( rb.IsValid() )
			rb.MotionEnabled = _motionWasEnabled;
	}

	void ApplyVisualState( bool force = false )
	{
		if ( !force && _appliedVisual == IsFaded )
			return;

		_appliedVisual = IsFaded;

		foreach ( var renderer in GameObject.GetComponentsInChildren<ModelRenderer>( true ) )
		{
			if ( !renderer.IsValid() )
				continue;

			if ( IsFaded )
			{
				if ( !_originalTints.ContainsKey( renderer ) )
					_originalTints[renderer] = renderer.Tint;

				if ( NoEffect )
				{
					renderer.Tint = Color.White.WithAlpha( 0f );
					renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
				}
				else
				{
					renderer.Tint = FadedTint;
					renderer.RenderType = ModelRenderer.ShadowRenderType.On;
				}
			}
			else
			{
				if ( _originalTints.TryGetValue( renderer, out var tint ) )
					renderer.Tint = tint;
				else
					renderer.Tint = Color.White;

				renderer.RenderType = ModelRenderer.ShadowRenderType.On;
			}
		}

		if ( !IsFaded )
			_originalTints.Clear();
	}

	public void Configure( string fadeKey, bool toggle, bool startFaded, bool noEffect )
	{
		FadeKey = fadeKey ?? "";
		Toggle = toggle;
		StartFaded = startFaded;
		NoEffect = noEffect;

		if ( Networking.IsHost )
		{
			if ( startFaded )
				SetFaded( true );
			else if ( IsFaded )
				SetFaded( false );
		}
	}
}
