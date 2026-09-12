public sealed class WeaponShipmentItemDefinition
{
	public WeaponShipmentItemDefinition( string weaponPrefabPath, string title, int price, string description, int weaponsPerShipment = 10, bool gunDealerOnly = true )
	{
		WeaponPrefabPath = weaponPrefabPath;
		Title = title;
		Price = price;
		Description = description;
		WeaponsPerShipment = weaponsPerShipment;
		GunDealerOnly = gunDealerOnly;
	}

	public string WeaponPrefabPath { get; }
	public string Title { get; }
	public int Price { get; }
	public string Description { get; }
	public int WeaponsPerShipment { get; }
	public bool GunDealerOnly { get; }
}

public static class WeaponShipmentCatalog
{
	static readonly WeaponShipmentItemDefinition[] Items =
	[
		new( "weapons/glock/glock.prefab", "USP", 4800, "A crate with 10 USP pistols for resale.", WeaponShipment.MaxStackSize, true ),
		new( "weapons/colt1911/colt1911.prefab", "1911", 6000, "A crate with 10 Colt 1911 pistols for resale.", WeaponShipment.MaxStackSize, true ),
		new( "weapons/mp5/mp5.prefab", "SMG", 12800, "A crate with 10 SMGs ready to distribute.", WeaponShipment.MaxStackSize, true ),
		new( "weapons/shotgun/shotgun.prefab", "Shotgun", 16800, "A crate with 10 shotguns for close-range muscle.", WeaponShipment.MaxStackSize, true ),
		new( "weapons/m4a1/m4a1.prefab", "M4A1", 20800, "A crate with 10 M4A1 rifles for heavier loadouts.", WeaponShipment.MaxStackSize, true ),
		new( "weapons/sniper/sniper.prefab", "Sniper", 25600, "A crate with 10 sniper rifles for long sightlines.", WeaponShipment.MaxStackSize, true ),
		new( "weapons/rpg/rpg.prefab", "Rocket Launcher", 80000, "A crate with 10 rocket launchers for high-end weapon dealing.", WeaponShipment.MaxStackSize, true )
	];

	public static IReadOnlyList<WeaponShipmentItemDefinition> GetAll()
	{
		return Items;
	}

	public static WeaponShipmentItemDefinition Get( string weaponPrefabPath )
	{
		if ( string.IsNullOrWhiteSpace( weaponPrefabPath ) )
			return null;

		return Items.FirstOrDefault( x => string.Equals( x.WeaponPrefabPath, weaponPrefabPath, StringComparison.OrdinalIgnoreCase ) );
	}

	public static bool ShouldShowInShop( Player player, WeaponShipmentItemDefinition item )
	{
		if ( item is null )
			return false;

		return !item.GunDealerOnly || WeaponShopCatalog.IsGunDealer( player );
	}

	public static bool CanPlayerBuy( Player player, string weaponPrefabPath, out string reason )
	{
		reason = null;

		var item = Get( weaponPrefabPath );
		if ( item is null )
		{
			reason = "Unknown shipment.";
			return false;
		}

		if ( !item.GunDealerOnly )
			return true;

		if ( player is null )
		{
			reason = "Player unavailable.";
			return false;
		}

		if ( !WeaponShopCatalog.IsGunDealer( player ) )
		{
			reason = "Gun Dealer only.";
			return false;
		}

		return true;
	}
}
