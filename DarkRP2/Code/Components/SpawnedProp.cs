using Sandbox;

/// <summary>
/// Marks props created by <see cref="PropSpawner"/> so prop limits only count player-spawned props.
/// </summary>
public sealed class SpawnedProp : Component
{
	protected override void OnDestroy()
	{
		if ( Networking.IsHost )
		{
			var owner = GameObject.Components.Get<Ownable>()?.Owner;
			var player = Player.FindForConnection( owner );
			player?.SendPropLimitNotice( this );
		}

		base.OnDestroy();
	}
}
