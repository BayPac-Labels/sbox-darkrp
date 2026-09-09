using Sandbox;
using System.Text.Json.Serialization;

/// <summary>
/// Marks objects created by toolgun modes that should count toward per-tool spawn limits.
/// </summary>
public sealed class ToolSpawnedObject : Component
{
	[Sync( SyncFlags.FromHost )]
	private Guid _ownerId { get; set; }

	[Property, Sync( SyncFlags.FromHost ), ReadOnly]
	public string ToolKey { get; private set; }

	[Property, Sync( SyncFlags.FromHost ), ReadOnly]
	public string ToolName { get; private set; }

	[Property, ReadOnly, JsonIgnore]
	public Connection Owner
	{
		get => Connection.All.FirstOrDefault( c => c.Id == _ownerId );
		private set => _ownerId = value?.Id ?? Guid.Empty;
	}

	public static ToolSpawnedObject Set( GameObject go, Connection owner, string toolKey, string toolName, bool assignOwnable = true )
	{
		var marker = go.GetOrAddComponent<ToolSpawnedObject>();
		marker.Owner = owner;
		marker.ToolKey = toolKey;
		marker.ToolName = string.IsNullOrWhiteSpace( toolName ) ? toolKey : toolName;

		if ( assignOwnable )
		{
			Ownable.Set( go, owner );
		}

		return marker;
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost )
		{
			var player = Player.FindForConnection( Owner );
			player?.SendToolSpawnLimitNotice( ToolKey, ToolName, this );
		}

		base.OnDestroy();
	}
}
