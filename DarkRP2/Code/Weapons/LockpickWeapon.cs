public sealed class LockpickWeapon : MeleeWeapon
{
	public override void OnControl( Player player )
	{
		if ( !player.IsValid() || !player.GameObject.IsValid() )
			return;

		player.HandleDoorLockpickInput();
	}
}
