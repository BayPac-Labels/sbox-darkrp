public static class JobManager
{
	public static int CountPlayers( JobDefinition definition )
	{
		return CountPlayers( definition?.ResourcePath );
	}

	public static int CountPlayers( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || Game.ActiveScene is null )
			return 0;

		return Game.ActiveScene.GetAll<PlayerData>()
			.Count( x => string.Equals( x.JobDefinitionPath, resourcePath, StringComparison.OrdinalIgnoreCase ) );
	}

	public static bool CanJoin( Player player, JobDefinition definition, out string reason )
	{
		reason = null;

		if ( player is null || definition is null )
		{
			reason = "Invalid job selection.";
			return false;
		}

		if ( string.Equals( player.JobDefinitionPath, definition.ResourcePath, StringComparison.OrdinalIgnoreCase ) )
			return true;

		if ( definition.MaxPlayers > 0 && CountPlayers( definition ) >= definition.MaxPlayers )
		{
			reason = "This job is full.";
			return false;
		}

		return true;
	}

	public static string FormatSlots( JobDefinition definition )
	{
		var count = CountPlayers( definition );
		return definition?.MaxPlayers > 0 ? $"{count} / {definition.MaxPlayers}" : $"{count} / Unlimited";
	}
}
