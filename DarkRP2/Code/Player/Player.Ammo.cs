using Sandbox.UI;

public sealed partial class Player
{
	[Rpc.Host]
	public void RequestBuyAmmo( string prefabPath )
	{
		var definition = AmmoShopCatalog.Get( prefabPath );
		if ( Rpc.Caller != Network.Owner || definition is null )
			return;

		if ( !AmmoShopCatalog.CanPlayerBuy( this, prefabPath, out var restrictionReason ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, restrictionReason, 3 );
			return;
		}

		if ( !AmmoShopCatalog.TryGetPickupAmmo( definition.PrefabPath, out _, out _ ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "Ammo pickup unavailable.", 3 );
			return;
		}

		if ( !TryTakeMoney( definition.Price ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "You don't have enough money.", 3 );
			return;
		}

		if ( AmmoPickup.TrySpawn( this, definition.PrefabPath ) )
		{
			Notices.SendNotice( Network.Owner, "$", Color.Green, $"{definition.Title} purchased.", 3 );
			return;
		}

		GiveMoney( definition.Price );
		Notices.SendNotice( Network.Owner, "block", Color.Red, "Unable to place that ammo right now.", 3 );
	}
}
