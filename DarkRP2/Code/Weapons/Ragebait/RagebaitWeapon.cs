using Sandbox.Rendering;

public sealed class RagebaitWeapon : BaseWeapon
{
	const string ThrowSoundPath = "weapons/crowbar/sounds/crowbar.swing.sound";
	const string InventoryIconPath = "thumb:models/props/fruit/pomegranate.vmdl";

	public override string InventoryIconOverride => InventoryIconPath;

	[Property] public GameObject ProjectilePrefab { get; set; }
	[Property] public float ThrowPower { get; set; } = 1450.0f;
	[Property] public float UpwardThrowPower { get; set; } = 75.0f;
	[Property] public float Cooldown { get; set; } = 2.0f;
	[Property] public float Damage { get; set; } = 8.0f;
	[Property] public float ImpactForce { get; set; } = 450.0f;
	[Property] public float ProjectileLifetime { get; set; } = 8.0f;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	protected override float GetPrimaryFireRate() => Cooldown;

	public override void PrimaryAttack()
	{
		if ( !Owner.IsValid() )
			return;

		ThrowProjectile( Owner );
		AddShootDelay( Cooldown );
	}

	[Rpc.Host]
	void ThrowProjectile( Player player )
	{
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner || player != Owner )
			return;

		if ( !ProjectilePrefab.IsValid() )
			return;

		var eye = player.EyeTransform;
		var direction = eye.Rotation.Forward;
		var startPosition = GetThrowPosition( player, direction );
		var projectileObject = ProjectilePrefab.Clone( startPosition );

		var projectile = projectileObject.GetOrAddComponent<RagebaitProjectile>();
		projectile.Attacker = player.GameObject;
		projectile.Damage = Damage;
		projectile.ImpactForce = ImpactForce;
		projectile.Lifetime = ProjectileLifetime;

		if ( projectileObject.GetComponent<Rigidbody>() is { } rb )
		{
			rb.Velocity = player.Controller.Velocity + direction * ThrowPower + Vector3.Up * UpwardThrowPower;
			rb.AngularVelocity = Vector3.Random * 10.0f;
		}

		var filter = projectileObject.AddComponent<PhysicsFilter>();
		filter.Body = player.GameObject;

		projectileObject.NetworkSpawn();
		PlayThrowEffects();
	}

	Vector3 GetThrowPosition( Player player, Vector3 direction )
	{
		var eye = player.EyeTransform;
		var target = eye.Position + direction * 24.0f + eye.Rotation.Right * 6.0f;

		var trace = Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, 8.0f ), eye.Position, target )
			.WithoutTags( "trigger", "ragdoll" )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		return trace.Hit ? trace.EndPosition : target;
	}

	[Rpc.Broadcast]
	void PlayThrowEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		Owner?.Controller?.Renderer?.Set( "b_attack", true );
		Owner?.Controller?.Renderer?.Set( "b_throw", true );
		Owner?.Controller?.Renderer?.Set( "throw_type", 0 );
		Owner?.Controller?.Renderer?.Set( "throw_blend", 0.0f );
		WeaponModel?.Renderer?.Set( "b_attack", true );
		WeaponModel?.Renderer?.Set( "b_throw", true );
		WeaponModel?.Renderer?.Set( "throw_type", 0 );
		WeaponModel?.Renderer?.Set( "throw_blend", 0.0f );

		Invoke( 0.45f, () =>
		{
			Owner?.Controller?.Renderer?.Set( "b_throw", false );
			WeaponModel?.Renderer?.Set( "b_deploy_new", true );
			WeaponModel?.Renderer?.Set( "b_throw", false );
			WeaponModel?.Renderer?.Set( "b_attack", false );
			WeaponModel?.Renderer?.Set( "b_charge", false );
			WeaponModel?.Renderer?.Set( "b_pull", false );
			WeaponModel?.Deploy();
		} );

		Invoke( 0.65f, () =>
		{
			DestroyViewModel();
			CreateViewModel();
		} );

		GameObject.PlaySound( ResourceLibrary.Get<SoundEvent>( ThrowSoundPath ) );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawCircle( center, 5.0f, color );
	}
}
