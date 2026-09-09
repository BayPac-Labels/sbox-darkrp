public sealed class WeaponShopItemDefinition
{
	public WeaponShopItemDefinition( string prefabPath, string title, int price, string description, bool gunDealerOnly = false )
	{
		PrefabPath = prefabPath;
		Title = title;
		Price = price;
		Description = description;
		GunDealerOnly = gunDealerOnly;
	}

	public string PrefabPath { get; }
	public string Title { get; }
	public int Price { get; }
	public string Description { get; }
	public bool GunDealerOnly { get; }
}

public static class WeaponShopCatalog
{
	public const string GunDealerJobDefinitionPath = "jobs/gun_dealer.jobdef";

	static readonly WeaponShopItemDefinition[] Items =
	[
		new( "weapons/crowbar/crowbar.prefab", "Crowbar", 250, "A cheap melee option that hits hard at point-blank range." ),
		new( "weapons/glock/glock.prefab", "USP", 600, "A dependable sidearm for cheap, accurate close-range fights." ),
		new( "weapons/colt1911/colt1911.prefab", "1911", 750, "A heavier pistol with stronger shots and a smaller magazine." ),
		new( "weapons/grenade/grenade.prefab", "Grenade", 900, "A thrown explosive for flushing players out of tight positions.", true ),
		new( "weapons/mp5/mp5.prefab", "SMG", 1600, "A fast-firing SMG built for aggressive short-range pressure.", true ),
		new( "weapons/shotgun/shotgun.prefab", "Shotgun", 2100, "A close-quarters weapon that deals massive damage up close.", true ),
		new( "weapons/m4a1/m4a1.prefab", "M4A1", 2600, "A balanced assault rifle that stays effective in most fights.", true ),
		new( "weapons/sniper/sniper.prefab", "Sniper", 3200, "A high-damage rifle made for long-range picks and hold angles.", true ),
		new( "weapons/rpg/rpg.prefab", "Rocket Launcher", 10000, "A heavy launcher for expensive, high-impact explosive pressure.", true )
	];

	public static IReadOnlyList<WeaponShopItemDefinition> GetAll()
	{
		return Items;
	}

	public static WeaponShopItemDefinition Get( string prefabPath )
	{
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
			return null;

		return Items.FirstOrDefault( x => string.Equals( x.PrefabPath, prefabPath, StringComparison.OrdinalIgnoreCase ) );
	}

	public static bool ShouldShowInShop( Player player, WeaponShopItemDefinition item )
	{
		if ( item is null )
			return false;

		return !item.GunDealerOnly || IsGunDealer( player );
	}

	public static bool CanPlayerBuy( Player player, string prefabPath, out string reason )
	{
		reason = null;

		var item = Get( prefabPath );
		if ( item is null )
		{
			reason = "Unknown weapon.";
			return false;
		}

		if ( !item.GunDealerOnly )
			return true;

		if ( player is null )
		{
			reason = "Player unavailable.";
			return false;
		}

		if ( !IsGunDealer( player ) )
		{
			reason = "Gun Dealer only.";
			return false;
		}

		return true;
	}

	public static bool IsGunDealer( Player player )
	{
		var job = player?.CurrentJobDefinition;
		if ( job is null )
			return false;

		if ( string.Equals( job.ResourcePath, GunDealerJobDefinitionPath, StringComparison.OrdinalIgnoreCase ) )
			return true;

		if ( string.Equals( job.Command, "/gundealer", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return string.Equals( job.Title, "Gun Dealer", StringComparison.OrdinalIgnoreCase );
	}
}
