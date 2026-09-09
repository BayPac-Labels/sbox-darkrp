using Sandbox.UI;

public sealed partial class Player
{
	public const int ToolSpawnLimit = 20;
	const float ToolActionCooldownSeconds = 1.0f;
	const float ToolActionDeniedNoticeCooldownSeconds = 1.0f;

	TimeSince _timeSinceToolAction = ToolActionCooldownSeconds;
	TimeSince _timeSinceToolActionDeniedNotice = ToolActionDeniedNoticeCooldownSeconds;

	public int GetToolSpawnCount( string toolKey, ToolSpawnedObject ignoredObject = null )
	{
		var owner = Network.Owner;
		if ( owner is null || Game.ActiveScene is null || string.IsNullOrWhiteSpace( toolKey ) )
			return 0;

		return Game.ActiveScene.GetAllComponents<ToolSpawnedObject>()
			.Count( spawned => spawned.IsValid()
				&& spawned != ignoredObject
				&& spawned.GameObject.IsValid()
				&& string.Equals( spawned.ToolKey, toolKey, StringComparison.OrdinalIgnoreCase )
				&& spawned.Owner == owner );
	}

	public bool CanSpawnToolObject( string toolKey, string toolName, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
		{
			error = "Invalid tool request.";
			return false;
		}

		if ( HasAdminAccess )
			return true;

		if ( GetToolSpawnCount( toolKey ) < ToolSpawnLimit )
			return true;

		var name = string.IsNullOrWhiteSpace( toolName ) ? "tool" : toolName;
		error = $"{name} limit reached ({ToolSpawnLimit}/{ToolSpawnLimit}).";
		return false;
	}

	public void RegisterToolSpawnedObject( GameObject go, string toolKey, string toolName, bool assignOwnable = true )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null || !go.IsValid() )
			return;

		ToolSpawnedObject.Set( go, Network.Owner, toolKey, toolName, assignOwnable );
		SendToolSpawnLimitNotice( toolKey, toolName );
	}

	public void SendToolSpawnLimitNotice( string toolKey, string toolName, ToolSpawnedObject ignoredObject = null )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null || string.IsNullOrWhiteSpace( toolKey ) )
			return;

		var name = string.IsNullOrWhiteSpace( toolName ) ? "Tool" : toolName;
		var limit = HasAdminAccess ? "unlimited" : ToolSpawnLimit.ToString();
		Notices.SendNotice( Network.Owner, "construction", Color.Cyan, $"{name}: {GetToolSpawnCount( toolKey, ignoredObject )}/{limit}", 2 );
	}

	public bool TryUseToolActionCooldown( string toolName, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
		{
			error = "Invalid tool request.";
			return false;
		}

		if ( HasAdminAccess )
			return true;

		if ( _timeSinceToolAction >= ToolActionCooldownSeconds )
		{
			_timeSinceToolAction = 0;
			return true;
		}

		var remaining = MathF.Ceiling( (ToolActionCooldownSeconds - _timeSinceToolAction) * 10.0f ) / 10.0f;
		var name = string.IsNullOrWhiteSpace( toolName ) ? "this tool" : toolName;
		error = $"Slow down before using {name} again ({remaining:0.0}s).";
		return false;
	}

	public void SendToolActionDeniedNotice( string error )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null || string.IsNullOrWhiteSpace( error ) )
			return;

		if ( _timeSinceToolActionDeniedNotice < ToolActionDeniedNoticeCooldownSeconds )
			return;

		_timeSinceToolActionDeniedNotice = 0;
		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 2 );
	}
}
