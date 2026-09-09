using Sandbox.Rendering;

public sealed class MedkitWeapon : BaseWeapon
{
	private const string MedicJobDefinitionPath = "jobs/medic.jobdef";

	[Property, Group( "Healing" )] public float HealAmount { get; set; } = 4f;
	[Property, Group( "Healing" )] public float HealInterval { get; set; } = 0.25f;
	[Property, Group( "Healing" )] public float OverhealMaxHealth { get; set; } = 130f;
	[Property, Group( "Healing" )] public float HealRange { get; set; } = 128f;
	[Property, Group( "Healing" )] public float TraceRadius { get; set; } = 10f;
	[Property, Group( "Healing" )] public SoundEvent HealSound { get; set; }

	private TimeUntil timeUntilServerHeal;

	public override bool CanPrimaryAttack()
	{
		if ( IsReloading() ) return false;
		return TimeUntilNextShotAllowed <= 0;
	}

	public override bool CanSecondaryAttack()
	{
		if ( IsReloading() ) return false;
		return TimeUntilNextShotAllowed <= 0;
	}

	public override void PrimaryAttack()
	{
		AddShootDelay( HealInterval );
		RequestHeal( false );
	}

	public override void SecondaryAttack()
	{
		AddShootDelay( HealInterval );
		RequestHeal( true );
	}

	[Rpc.Host]
	private void RequestHeal( bool selfHeal )
	{
		var player = Owner;
		if ( !player.IsValid() || !player.GameObject.IsValid() )
			return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() || inventory.ActiveWeapon != this )
			return;

		if ( timeUntilServerHeal > 0 )
			return;

		timeUntilServerHeal = HealInterval;

		var target = selfHeal ? player : FindTarget( player );
		if ( !target.IsValid() )
			return;

		var maxHealth = GetHealLimit( player, target );
		if ( target.Health <= 0f || maxHealth <= 0f || target.Health >= maxHealth )
			return;

		var newHealth = (target.Health + HealAmount).Clamp( 0f, maxHealth );
		if ( newHealth <= target.Health )
			return;

		target.Health = newHealth;
		HealEffects( target.GameObject );
	}

	private float GetHealLimit( Player healer, Player target )
	{
		if ( healer.IsValid() && string.Equals( healer.JobDefinitionPath, MedicJobDefinitionPath, StringComparison.OrdinalIgnoreCase ) )
			return MathF.Max( target.MaxHealth, OverhealMaxHealth );

		return target.MaxHealth;
	}

	private Player FindTarget( Player player )
	{
		var forward = player.EyeTransform.Rotation.Forward;
		var trace = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, HealRange )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "playercontroller" )
			.Radius( TraceRadius )
			.UseHitboxes()
			.Run();

		if ( !trace.GameObject.IsValid() )
			return null;

		var target = trace.GameObject.GetComponentInParent<Player>();
		if ( !target.IsValid() || target == player )
			return null;

		return target;
	}

	[Rpc.Broadcast]
	private void HealEffects( GameObject targetObject )
	{
		if ( Application.IsDedicatedServer )
			return;

		var player = Owner;
		if ( player.IsValid() )
			player.Controller.Renderer.Set( "b_attack", true );

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );

		if ( HealSound.IsValid() && targetObject.IsValid() )
			targetObject.PlaySound( HealSound );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : new Color( 0.35f, 1f, 0.55f );

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left * 10f, center - Vector2.Left * 10f, 3f, color );
		hud.DrawLine( center + Vector2.Up * 10f, center - Vector2.Up * 10f, 3f, color );
	}
}
