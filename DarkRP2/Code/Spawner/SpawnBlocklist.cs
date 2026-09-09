public static class SpawnBlocklist
{
	static readonly HashSet<string> BlockedProps =
	[
		NormalizePackageIdent( "facepunch/oildrumexplosive" )
	];

	public static bool IsBlockedIdentForPlayer( Player player, string ident )
	{
		var (type, path, _) = SpawnlistItem.ParseIdent( ident );
		return IsBlockedForPlayer( player, type, path );
	}

	public static bool IsBlockedForPlayer( Player player, string type, string path )
	{
		if ( !player.IsValid() || player.HasAdminAccess )
			return false;

		if ( !string.Equals( type, "prop", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return BlockedProps.Contains( NormalizePackageIdent( path ) );
	}

	static string NormalizePackageIdent( string ident )
	{
		if ( string.IsNullOrWhiteSpace( ident ) )
			return string.Empty;

		var value = ident.Trim().ToLowerInvariant();
		value = value.TrimEnd( '/' );

		const string urlPrefix = "https://sbox.game/";
		if ( value.StartsWith( urlPrefix ) )
		{
			value = value[urlPrefix.Length..];
		}

		return value.Replace( '.', '/' );
	}
}
