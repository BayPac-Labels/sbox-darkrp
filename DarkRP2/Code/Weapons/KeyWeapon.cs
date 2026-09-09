public sealed class KeyWeapon : MeleeWeapon
{
	public override void OnControl( Player player )
	{
		if ( !player.IsValid() || !player.GameObject.IsValid() )
			return;

		player.HandleDoorKeyInput();
	}
}
