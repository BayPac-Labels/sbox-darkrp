public sealed class ArrestStickWeapon : MeleeWeapon
{
	protected override bool WantsPrimaryAttack() => Input.Pressed( "attack1" );
	bool WantsSecondaryAttack() => Input.Pressed( "attack2" );

	public override void OnControl( Player player )
	{
		if ( !player.IsValid() || !player.GameObject.IsValid() )
			return;

		if ( WantsPrimaryAttack() && CanAttack() )
		{
			TryArrestTarget( player );
			Swing( player );
			return;
		}

		if ( WantsSecondaryAttack() && CanAttack() )
		{
			TryReleaseTarget( player );
			Swing( player );
		}
	}

	void TryArrestTarget( Player player )
	{
		if ( !player.IsValid() || !player.GameObject.IsValid() || !player.CanArrestPlayers )
			return;

		var target = FindTarget( player );
		if ( !target.IsValid() || target == player )
			return;

		player.RequestArrestPlayer( target.PlayerId );
	}

	void TryReleaseTarget( Player player )
	{
		if ( !player.IsValid() || !player.GameObject.IsValid() || !player.CanArrestPlayers )
			return;

		var target = FindTarget( player );
		if ( !target.IsValid() || target == player )
			return;

		player.RequestReleaseArrestPlayer( target.PlayerId );
	}

	Player FindTarget( Player player )
	{
		var forward = player.EyeTransform.Rotation.Forward;
		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, Range )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "playercontroller" )
			.Radius( SwingRadius )
			.UseHitboxes()
			.Run();

		if ( !tr.GameObject.IsValid() )
			return null;

		var targetRoot = tr.GameObject.Root;
		if ( !targetRoot.IsValid() )
			return null;

		return targetRoot.GetComponent<Player>();
	}
}
