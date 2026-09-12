namespace Sandbox.UI;

/// <summary>
/// Click-to-bind control for raw keyboard keys. Displays the key character, not a game action name.
/// </summary>
[CustomEditor( typeof( string ), WithAllAttributes = [typeof( KeyBindAttribute )] )]
public class KeyBindControl : BaseControl
{
	Panel _preview;
	Label _keyGlyph;
	IconPanel _fallbackIcon;
	Label _bindLabel;
	KeyCaptureEntry _capture;
	bool _listening;
	RealTimeSince _listenStarted;
	HashSet<string> _heldAtStart;

	/// <summary>
	/// Ignore keys for a short window after clicking bind so focus / held movement keys don't auto-bind.
	/// </summary>
	const float ArmDelay = 0.15f;

	static readonly HashSet<string> IgnoredButtons = new( StringComparer.OrdinalIgnoreCase )
	{
		"escape",
		"mouse1",
		"mouse2",
		"mouse3",
		"mouse4",
		"mouse5",
		"mwheelup",
		"mwheeldown",
	};

	public override bool SupportsMultiEdit => true;

	public KeyBindControl()
	{
		AcceptsFocus = true;

		_preview = AddChild<Panel>( "preview" );
		_keyGlyph = _preview.AddChild<Label>( "key-glyph" );
		_fallbackIcon = _preview.AddChild<IconPanel>( "fallback" );
		_fallbackIcon.Text = "keyboard";

		_bindLabel = AddChild<Label>( "bind-label" );
		_bindLabel.SetClass( "hidden", true );

		// TextEntry receives button events reliably; kept non-interactive until listening.
		_capture = AddChild<KeyCaptureEntry>( "key-capture" );
		_capture.OnKeyBound = OnCaptureKey;
		_capture.AcceptsFocus = true;
		_capture.Style.PointerEvents = PointerEvents.None;
	}

	public override void OnDeleted()
	{
		if ( Active == this )
			Active = null;

		base.OnDeleted();
	}

	public override void Rebuild()
	{
		if ( Property is null )
			return;

		if ( _listening )
		{
			_keyGlyph.Text = "?";
			_keyGlyph.SetClass( "hidden", false );
			_keyGlyph.SetClass( "compact", false );
			_fallbackIcon.SetClass( "hidden", true );
			_bindLabel.Text = "Press a key…";
			_bindLabel.SetClass( "hidden", false );
			SetClass( "no-binding", false );
			SetClass( "listening", true );
			return;
		}

		SetClass( "listening", false );
		_bindLabel.SetClass( "hidden", true );

		var key = Property.GetValue<string>();
		if ( string.IsNullOrWhiteSpace( key ) )
		{
			_keyGlyph.SetClass( "hidden", true );
			_keyGlyph.SetClass( "compact", false );
			_fallbackIcon.SetClass( "hidden", false );
			SetClass( "no-binding", true );
			return;
		}

		var display = FormatKey( key );
		_keyGlyph.Text = display;
		_keyGlyph.SetClass( "hidden", false );
		_keyGlyph.SetClass( "compact", display.Length > 1 );
		_fallbackIcon.SetClass( "hidden", true );
		SetClass( "no-binding", false );
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );
		e.StopPropagation();

		if ( e.MouseButton == MouseButtons.Right )
		{
			StopListening();
			SetKey( "" );
			return;
		}

		if ( _listening )
		{
			StopListening();
			Rebuild();
			return;
		}

		StartListening();
	}

	public override void Tick()
	{
		base.Tick();
		PollWhileListening();
	}

	/// <summary>
	/// Also called from the active tool so capture still works if this panel's Tick is skipped.
	/// </summary>
	public void PollWhileListening()
	{
		if ( !_listening )
			return;

		// Keep capture focused so OnButtonTyped keeps firing.
		if ( _capture is not null && !_capture.HasFocus )
			_capture.Focus();

		ReleaseHeldKeysThatAreUp();

		if ( _listenStarted > 15f )
		{
			StopListening();
			Rebuild();
			return;
		}

		if ( !IsArmed )
			return;

		// Clicking elsewhere in the submenu cancels bind mode (mouse1 is ignored for binding).
		if ( Input.Pressed( "mouse1" ) && !HasHovered )
		{
			StopListening();
			Rebuild();
			return;
		}

		TryCaptureKeyboard();
	}

	bool IsArmed => _listenStarted >= ArmDelay;

	void OnCaptureKey( string button )
	{
		if ( !_listening || !IsArmed )
			return;

		if ( string.Equals( button, "escape", StringComparison.OrdinalIgnoreCase ) )
		{
			StopListening();
			Rebuild();
			return;
		}

		if ( string.IsNullOrWhiteSpace( button ) || IgnoredButtons.Contains( button ) )
			return;

		// Drop keys that were held at click but have since been released, so a fresh press can bind.
		ReleaseHeldKeysThatAreUp();
		if ( IsBlockedByHeldStart( button ) )
			return;

		// Stop first so SetKey → Rebuild shows the bound key instead of staying on "Press a key…".
		StopListening();
		SetKey( NormalizeKey( button ) );
	}

	void TryCaptureKeyboard()
	{
		ReleaseHeldKeysThatAreUp();

		foreach ( var key in CandidateKeys )
		{
			if ( !Input.Keyboard.Pressed( key ) )
				continue;

			if ( IgnoredButtons.Contains( key ) )
				continue;

			if ( IsBlockedByHeldStart( key ) )
				continue;

			StopListening();
			SetKey( NormalizeKey( key ) );
			return;
		}

		if ( Input.Keyboard.Pressed( "ESCAPE" ) || Input.EscapePressed )
		{
			Input.EscapePressed = false;
			StopListening();
			Rebuild();
		}
	}

	void StartListening()
	{
		_listening = true;
		_listenStarted = 0;
		Active = this;
		SnapshotHeldKeys();

		if ( _capture is not null )
		{
			_capture.Text = "";
			// Allow it to take focus, but keep pointer-events none so clicks still hit us to cancel.
			_capture.Style.PointerEvents = PointerEvents.None;
			_capture.Focus();
		}

		Rebuild();
	}

	void StopListening()
	{
		_listening = false;
		_heldAtStart = null;
		if ( Active == this )
			Active = null;

		if ( _capture is not null )
		{
			_capture.Blur();
			_capture.Text = "";
			_capture.Style.PointerEvents = PointerEvents.None;
		}

		Blur();
	}

	void SnapshotHeldKeys()
	{
		_heldAtStart = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var key in CandidateKeys )
		{
			if ( Input.Keyboard.Down( key ) )
				_heldAtStart.Add( key );
		}
	}

	void ReleaseHeldKeysThatAreUp()
	{
		if ( _heldAtStart is null || _heldAtStart.Count == 0 )
			return;

		_heldAtStart.RemoveWhere( key => !Input.Keyboard.Down( key ) );
	}

	/// <summary>
	/// Keys already held when binding started (e.g. W while walking) must not count until fully released.
	/// Call <see cref="ReleaseHeldKeysThatAreUp"/> first so a re-press after release can bind.
	/// </summary>
	bool IsBlockedByHeldStart( string button )
	{
		if ( _heldAtStart is null || _heldAtStart.Count == 0 )
			return false;

		var normalized = NormalizeKey( button );
		return _heldAtStart.Contains( button ) || _heldAtStart.Contains( normalized );
	}

	void SetKey( string key )
	{
		var normalized = string.IsNullOrWhiteSpace( key ) ? "" : key.Trim();
		Property.SetValue( normalized );

		foreach ( var target in Property.Parent?.Targets ?? Enumerable.Empty<object>() )
		{
			if ( target is Component component )
				GameManager.ChangeProperty( component, Property.Name, normalized );
		}

		Rebuild();
	}

	/// <summary>
	/// Currently listening control, so other systems can avoid treating the key as gameplay input.
	/// </summary>
	public static KeyBindControl Active { get; private set; }

	public static bool IsCapturing => Active is { _listening: true };

	static string NormalizeKey( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return "";

		key = key.Trim();

		// Single letters/digits stay lowercase to match Input.Keyboard docs ("a"-"z", "0"-"9").
		if ( key.Length == 1 )
			return key.ToLowerInvariant();

		return key.ToUpperInvariant();
	}

	/// <summary>
	/// Short bind label for the preview well. Always at most 3 characters.
	/// </summary>
	static string FormatKey( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return "";

		var normalized = key.Trim().Replace( " ", "" ).Replace( "_", "" ).ToUpperInvariant();
		if ( normalized.Length == 0 )
			return "";

		if ( Abbreviations.TryGetValue( normalized, out var abbrev ) )
			return abbrev;

		if ( normalized.Length <= 3 )
			return normalized;

		return normalized[..3];
	}

	static readonly Dictionary<string, string> Abbreviations = new( StringComparer.OrdinalIgnoreCase )
	{
		["SPACE"] = "SPC",
		["ENTER"] = "ENT",
		["RETURN"] = "ENT",
		["TAB"] = "TAB",
		["BACKSPACE"] = "BSP",
		["DEL"] = "DEL",
		["DELETE"] = "DEL",
		["INS"] = "INS",
		["INSERT"] = "INS",
		["HOME"] = "HOM",
		["END"] = "END",
		["PGUP"] = "PGU",
		["PAGEUP"] = "PGU",
		["PGDN"] = "PGD",
		["PAGEDOWN"] = "PGD",
		["UPARROW"] = "UP",
		["DOWNARROW"] = "DN",
		["LEFTARROW"] = "LFT",
		["RIGHTARROW"] = "RGT",
		["CAPSLOCK"] = "CAP",
		["NUMLOCK"] = "NUM",
		["SCROLLLOCK"] = "SCR",
		["ESCAPE"] = "ESC",
		["PAUSE"] = "PAU",
		["SHIFT"] = "SFT",
		["LSHIFT"] = "SFT",
		["RSHIFT"] = "RSF",
		["ALT"] = "ALT",
		["LALT"] = "ALT",
		["RALT"] = "RAL",
		["CTRL"] = "CTL",
		["CONTROL"] = "CTL",
		["LCONTROL"] = "CTL",
		["LCTRL"] = "CTL",
		["RCONTROL"] = "RCT",
		["RCTRL"] = "RCT",
		["LWIN"] = "WIN",
		["RWIN"] = "RWN",
		["SEMICOLON"] = ";",
		["KP0"] = "N0",
		["KP1"] = "N1",
		["KP2"] = "N2",
		["KP3"] = "N3",
		["KP4"] = "N4",
		["KP5"] = "N5",
		["KP6"] = "N6",
		["KP7"] = "N7",
		["KP8"] = "N8",
		["KP9"] = "N9",
		["KPDIVIDE"] = "N/",
		["KPMULTIPLY"] = "N*",
		["KPMINUS"] = "N-",
		["KPPLUS"] = "N+",
		["KPENTER"] = "NEN",
		["KPDEL"] = "NDL",
		["MOUSE1"] = "M1",
		["MOUSELEFT"] = "M1",
		["MOUSE2"] = "M2",
		["MOUSERIGHT"] = "M2",
		["MOUSE3"] = "M3",
		["MOUSEMIDDLE"] = "M3",
		["MOUSE4"] = "M4",
		["MOUSE5"] = "M5",
		["MOUSEBACK"] = "MBK",
		["MOUSEFORWARD"] = "MFW",
		["MWHEELUP"] = "MWU",
		["MWHEELDOWN"] = "MWD",
	};

	/// <summary>
	/// Raw keyboard names from https://sbox.game/dev/doc/gameplay/input/raw-input
	/// </summary>
	static readonly string[] CandidateKeys =
	[
		"0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
		"a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
		"n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
		"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
		"SPACE", "ENTER", "TAB", "BACKSPACE", "DEL", "INS", "HOME", "END",
		"PGUP", "PGDN", "UPARROW", "DOWNARROW", "LEFTARROW", "RIGHTARROW",
		"KP_0", "KP_1", "KP_2", "KP_3", "KP_4", "KP_5", "KP_6", "KP_7", "KP_8", "KP_9",
		"KP_DIVIDE", "KP_MULTIPLY", "KP_MINUS", "KP_PLUS", "KP_ENTER", "KP_DEL",
		"SEMICOLON", ",", ".", "/", "\\", "-", "=", "[", "]", "'", "`",
		"SHIFT", "RSHIFT", "ALT", "RALT",
	];

	/// <summary>
	/// Invisible TextEntry used only to receive <see cref="Panel.OnButtonTyped"/> while binding.
	/// </summary>
	class KeyCaptureEntry : TextEntry
	{
		public Action<string> OnKeyBound { get; set; }

		public KeyCaptureEntry()
		{
			HasClearButton = false;
			Multiline = false;
		}

		public override void OnButtonTyped( ButtonEvent e )
		{
			if ( OnKeyBound is null )
			{
				base.OnButtonTyped( e );
				return;
			}

			e.StopPropagation = true;

			// Only bind on key-down. Releases (and focus delivering a held key's release) used to bind W.
			if ( !e.Pressed )
				return;

			if ( string.IsNullOrWhiteSpace( e.Button ) )
				return;

			// Never insert characters into the field — we only care about the key name.
			Text = "";
			OnKeyBound.Invoke( e.Button );
		}

		public override void OnKeyTyped( char k )
		{
			if ( OnKeyBound is null )
			{
				base.OnKeyTyped( k );
				return;
			}

			if ( char.IsControl( k ) || char.IsWhiteSpace( k ) && k != ' ' )
				return;

			Text = "";

			if ( k == ' ' )
			{
				OnKeyBound.Invoke( "SPACE" );
				return;
			}

			OnKeyBound.Invoke( k.ToString() );
		}

		protected override void OnBlur( PanelEvent e )
		{
			base.OnBlur( e );
			Text = "";
		}
	}
}
