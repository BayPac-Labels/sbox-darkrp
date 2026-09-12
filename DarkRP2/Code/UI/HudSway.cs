namespace Sandbox.UI;

/// <summary>
/// Shared walk / run / jump sway for player and toolbar HUD panels.
/// </summary>
public static class HudSway
{
	const float WalkAmplitude = 3.2f;
	const float RunAmplitude = 6.0f;
	const float BobFrequency = 9.0f;
	const float AirAmplitude = 0.045f;
	const float LandPunch = 11f;
	const float RollAmplitude = 0.4f;
	const float SmoothRate = 16f;

	static double _lastNow = -1;
	static float _cycle;
	static float _land;
	static bool _wasGrounded = true;
	static Vector2 _current;
	static float _roll;

	public static Vector2 Offset => _current;
	public static float Roll => _roll;

	/// <summary>
	/// Advances sway once per frame from the local player's movement.
	/// </summary>
	public static void Tick()
	{
		if ( _lastNow == Time.Now )
			return;

		_lastNow = Time.Now;

		if ( !GamePreferences.ViewBobbing )
		{
			Settle( Time.Delta * SmoothRate );
			_land = 0f;
			return;
		}

		var player = Player.FindLocalPlayer();
		if ( !player.IsValid() || !player.Controller.IsValid() )
		{
			Settle( Time.Delta * SmoothRate );
			return;
		}

		var controller = player.Controller;
		var velocity = controller.Velocity;
		var groundSpeed = velocity.WithZ( 0 ).Length;
		var onGround = controller.IsOnGround;
		var runSpeed = MathF.Max( controller.RunSpeed, 1f );
		var walkSpeed = MathF.Max( runSpeed * 0.5f, 1f );

		if ( onGround && !_wasGrounded )
		{
			var impact = MathF.Abs( velocity.z ).Remap( 80f, 450f, 0.35f, 1.6f );
			_land = LandPunch * impact;
		}

		_wasGrounded = onGround;
		_land = MathX.Lerp( _land, 0f, Time.Delta * 9f, true );

		Vector2 target;
		float targetRoll;

		if ( onGround && groundSpeed > 18f )
		{
			var speedFrac = (groundSpeed / runSpeed).Clamp( 0f, 1.4f );
			var running = groundSpeed > walkSpeed * 1.15f;
			var amp = (running ? RunAmplitude : WalkAmplitude) * speedFrac.Clamp( 0.3f, 1.2f );
			var freq = BobFrequency * (0.7f + speedFrac * 0.55f);

			_cycle += Time.Delta * freq * (groundSpeed / walkSpeed).Clamp( 0.45f, 2.4f );

			var bobX = MathF.Sin( _cycle ) * amp;
			var bobY = MathF.Cos( _cycle * 2f ) * amp * 0.55f;

			// Subtle strafe lean so HUD tracks camera roll a little
			var strafe = controller.WishVelocity.Dot( player.EyeTransform.Left ) / -runSpeed;
			bobX += strafe.Clamp( -1f, 1f ) * amp * 0.35f;

			target = new Vector2( bobX, bobY + _land );
			targetRoll = MathF.Sin( _cycle ) * RollAmplitude * speedFrac + strafe * RollAmplitude;
		}
		else
		{
			var airY = onGround ? 0f : (-velocity.z * AirAmplitude).Clamp( -16f, 16f );
			target = new Vector2( 0f, airY + _land );
			targetRoll = 0f;

			if ( onGround )
				_cycle = MathX.Lerp( _cycle, 0f, Time.Delta * 5f, true );
		}

		var t = (Time.Delta * SmoothRate).Clamp( 0f, 1f );
		_current = Vector2.Lerp( _current, target, t );
		_roll = MathX.Lerp( _roll, targetRoll, t, true );
	}

	/// <summary>
	/// Applies the current sway transform to a panel. Call from OnUpdate.
	/// </summary>
	public static void Apply( Panel panel, float scale = 1f, Vector2 extraOffset = default )
	{
		if ( panel is null )
			return;

		Tick();

		// Whole-pixel translate keeps thin corner strokes crisp while walking.
		var x = MathF.Round( _current.x * scale + extraOffset.x );
		var y = MathF.Round( _current.y * scale + extraOffset.y );
		var roll = _roll * scale;

		var transform = new PanelTransform();
		transform.AddTranslate( Length.Pixels( x ), Length.Pixels( y ) );

		if ( MathF.Abs( roll ) > 0.001f )
			transform.AddRotation( 0f, 0f, roll );

		panel.Style.Transform = transform;
	}

	static void Settle( float t )
	{
		t = t.Clamp( 0f, 1f );
		_current = Vector2.Lerp( _current, Vector2.Zero, t );
		_roll = MathX.Lerp( _roll, 0f, t, true );
	}
}
