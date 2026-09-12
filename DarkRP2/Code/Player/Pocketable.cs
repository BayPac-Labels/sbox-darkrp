/// <summary>
/// Rules for which world / hotbar items can enter the RP pocket inventory.
/// Printers, gun shipments, and firearms — not props, wire, or job tools.
/// </summary>
public static class Pocketable
{
	/// <summary>
	/// Default sandbox tools that must never enter the RP pocket.
	/// They can still be rearranged on the hotbar.
	/// </summary>
	public static bool IsPermanentTool( BaseCarryable carryable )
	{
		return carryable is HandWeapon or KeyWeapon or Physgun or Toolgun;
	}

	public static bool IsRpGun( BaseCarryable carryable )
	{
		if ( !carryable.IsValid() )
			return false;

		if ( carryable.IsJobLocked || IsPermanentTool( carryable ) )
			return false;

		if ( carryable is SpawnerWeapon )
			return false;

		// Crowbar / arrest stick / lockpick etc.
		if ( carryable is MeleeWeapon )
			return false;

		if ( carryable is CameraWeapon or MedkitWeapon or ScreenWeapon )
			return false;

		return carryable is BaseWeapon;
	}

	public static bool IsPocketable( GameObject go )
	{
		if ( !go.IsValid() )
			return false;

		if ( go.GetComponent<MoneyPrinter>().IsValid() )
			return true;

		if ( go.GetComponent<WeaponShipment>().IsValid() )
			return true;

		var weapon = go.GetComponent<BaseCarryable>( true );
		return IsRpGun( weapon );
	}

	/// <summary>
	/// Walks up from a trace hit to the networked root that should be pocketed.
	/// </summary>
	public static GameObject ResolveRoot( GameObject hit )
	{
		if ( !hit.IsValid() )
			return null;

		for ( var go = hit; go.IsValid(); go = go.Parent )
		{
			if ( !IsPocketable( go ) )
				continue;

			var networkRoot = go.Network?.RootGameObject;
			return networkRoot.IsValid() ? networkRoot : go;
		}

		return null;
	}

	public static bool CanPlayerAccess( Player player, GameObject go )
	{
		if ( !player.IsValid() || !go.IsValid() )
			return false;

		var ownable = go.GetComponent<Ownable>();
		if ( !ownable.IsValid() )
			return true;

		return Ownable.HasProtectedAccess( player.Network.Owner, ownable.Owner );
	}
}
