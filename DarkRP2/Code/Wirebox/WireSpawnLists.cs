namespace Sandbox;

/// <summary>
/// Loads Wirebox <c>*.spawnlist</c> model entries for tool model pickers.
/// </summary>
public static class WireSpawnLists
{
	static readonly Dictionary<string, string[]> Cache = new( StringComparer.OrdinalIgnoreCase );

	public static IReadOnlyList<string> GetModels( params string[] listNames )
	{
		if ( listNames is null || listNames.Length == 0 )
			return Array.Empty<string>();

		var result = new List<string>();
		var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var name in listNames )
		{
			if ( string.IsNullOrWhiteSpace( name ) )
				continue;

			foreach ( var entry in LoadList( name ) )
			{
				if ( seen.Add( entry ) )
					result.Add( entry );
			}
		}

		return result;
	}

	static string[] LoadList( string listName )
	{
		var key = listName.Trim();
		if ( Cache.TryGetValue( key, out var cached ) )
			return cached;

		var paths = new[]
		{
			$"spawnlists/wirebox/wirebox.{key}.spawnlist",
			$"spawnlists/wirebox.{key}.spawnlist",
			$"wirebox/wirebox.{key}.spawnlist",
		};

		foreach ( var path in paths )
		{
			if ( !FileSystem.Mounted.FileExists( path ) )
				continue;

			var lines = FileSystem.Mounted.ReadAllText( path )
				.Split( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
				.Where( line => !string.IsNullOrWhiteSpace( line ) && !line.StartsWith( '#' ) )
				.ToArray();

			Cache[key] = lines;
			return lines;
		}

		Cache[key] = [];
		return Cache[key];
	}

	public static string DisplayName( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return "None";

		if ( path.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
			return System.IO.Path.GetFileNameWithoutExtension( path );

		var slash = path.LastIndexOf( '/' );
		var dot = path.LastIndexOf( '.' );
		if ( slash >= 0 && slash < path.Length - 1 )
			return path[(slash + 1)..];
		if ( dot >= 0 && dot < path.Length - 1 )
			return path[(dot + 1)..];

		return path;
	}

	public static string ThumbUrl( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		if ( path.Contains( '/' ) && path.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
			return $"thumb:{path}";

		// Cloud package ident — package thumb if available
		return $"thumb:{path}";
	}
}
