using Sandbox.UI;

public sealed partial class Player
{
	[Rpc.Host]
	public void RequestBuyWeaponShipment( string weaponPrefabPath )
	{
		var definition = WeaponShipmentCatalog.Get( weaponPrefabPath );
		if ( Rpc.Caller != Network.Owner || definition is null )
			return;

		if ( !WeaponShipmentCatalog.CanPlayerBuy( this, weaponPrefabPath, out var restrictionReason ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, restrictionReason, 3 );
			return;
		}

		if ( !TryTakeMoney( definition.Price ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "You don't have enough money.", 3 );
			return;
		}

		if ( WeaponShipment.TrySpawn( this, definition ) )
		{
			Notices.SendNotice( Network.Owner, "$", Color.Green, $"{definition.Title} purchased.", 3 );
			return;
		}

		GiveMoney( definition.Price );
		Notices.SendNotice( Network.Owner, "block", Color.Red, "Unable to place the shipment right now.", 3 );
	}
}
