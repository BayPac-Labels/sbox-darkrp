using System.Text.Json;

public static class PlayerRoleplayStorage
{
	const int CurrentVersion = 1;
	const string RootDirectory = "darkrp2/players";

	[ConVar( "drp.server_save_id", ConVarFlags.Replicated | ConVarFlags.GameSetting | ConVarFlags.Server, Help = "Unique identifier used for DarkRP player persistence on this host/server." )]
	public static string ServerSaveId { get; set; } = "";

	public static bool TryLoad( long steamId, out PlayerRoleplaySaveData data, out bool loadedFromBackup )
	{
		data = null;
		loadedFromBackup = false;

		if ( steamId <= 0 )
			return false;

		if ( TryRead( GetPrimaryPath( steamId ), out data ) )
			return true;

		if ( TryRead( GetBackupPath( steamId ), out data ) )
		{
			loadedFromBackup = true;
			return true;
		}

		return false;
	}

	public static bool Save( long steamId, PlayerRoleplaySaveData data )
	{
		if ( steamId <= 0 || data is null )
			return false;

		data.Version = CurrentVersion;
		data.SavedAtUtc = DateTime.UtcNow;

		var json = JsonSerializer.Serialize( data, new JsonSerializerOptions { WriteIndented = true } );
		var directory = GetServerDirectory();
		if ( !FileSystem.Data.DirectoryExists( directory ) )
		{
			FileSystem.Data.CreateDirectory( directory );
		}

		var backupPath = GetBackupPath( steamId );
		var primaryPath = GetPrimaryPath( steamId );

		try
		{
			FileSystem.Data.WriteAllText( backupPath, json );
		}
		catch ( Exception ex )
		{
			Log.Warning( ex, $"[PlayerRoleplayStorage] Failed to write backup '{backupPath}'." );
		}

		try
		{
			FileSystem.Data.WriteAllText( primaryPath, json );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( ex, $"[PlayerRoleplayStorage] Failed to write primary save '{primaryPath}'." );
			return false;
		}
	}

	static bool TryRead( string path, out PlayerRoleplaySaveData data )
	{
		data = null;

		if ( !FileSystem.Data.FileExists( path ) )
			return false;

		try
		{
			var json = FileSystem.Data.ReadAllText( path );
			var loaded = JsonSerializer.Deserialize<PlayerRoleplaySaveData>( json );
			if ( loaded is null || loaded.Version != CurrentVersion )
				return false;

			data = loaded;
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( ex, $"[PlayerRoleplayStorage] Failed to read save '{path}'." );
			return false;
		}
	}

	static string GetPrimaryPath( long steamId ) => $"{GetServerDirectory()}/{steamId}.json";
	static string GetBackupPath( long steamId ) => $"{GetServerDirectory()}/{steamId}.bak.json";
	static string GetServerDirectory() => $"{RootDirectory}/{ResolveServerKey()}";

	static string ResolveServerKey()
	{
		if ( !string.IsNullOrWhiteSpace( ServerSaveId ) )
			return SanitizePathSegment( ServerSaveId );

		var hostConnection = Connection.All.FirstOrDefault( x => x.IsHost );
		if ( hostConnection is not null && hostConnection.SteamId.Value > 0 )
			return $"host_{hostConnection.SteamId.Value}";

		return "host_local";
	}

	static string SanitizePathSegment( string value )
	{
		var chars = value
			.Trim()
			.Select( c => char.IsLetterOrDigit( c ) || c is '-' or '_' ? c : '_' )
			.ToArray();

		var sanitized = new string( chars ).Trim( '_' );
		return string.IsNullOrWhiteSpace( sanitized ) ? "host_local" : sanitized;
	}
}
