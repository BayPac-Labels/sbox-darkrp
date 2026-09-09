using Sandbox.UI;

public sealed partial class Player
{
	[Rpc.Host]
	public void RequestBuyWeapon( string prefabPath )
	{
		var definition = WeaponShopCatalog.Get( prefabPath );
		if ( Rpc.Caller != Network.Owner || definition is null )
			return;

		var inventory = GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "Inventory unavailable.", 3 );
			return;
		}

		if ( !WeaponShopCatalog.CanPlayerBuy( this, prefabPath, out var restrictionReason ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, restrictionReason, 3 );
			return;
		}

		if ( !TryTakeMoney( definition.Price ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "You don't have enough money.", 3 );
			return;
		}

		if ( inventory.Pickup( definition.PrefabPath ) )
		{
			Notices.SendNotice( Network.Owner, "$", Color.Green, $"{definition.Title} purchased.", 3 );
			return;
		}

		GiveMoney( definition.Price );
		Notices.SendNotice( Network.Owner, "block", Color.Red, "Unable to add that weapon right now.", 3 );
	}
}
