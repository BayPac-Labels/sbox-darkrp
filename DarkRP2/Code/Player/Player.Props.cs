using Sandbox.UI;

public sealed partial class Player
{
	public const int PropLimit = 300;
	const float PropSpawnCooldownSeconds = 1.0f;
	const float PropSpawnDeniedNoticeCooldownSeconds = 1.0f;

	TimeSince _timeSincePropSpawn = PropSpawnCooldownSeconds;
	TimeSince _timeSincePropSpawnDeniedNotice = PropSpawnDeniedNoticeCooldownSeconds;

	public bool HasUnlimitedProps => HasAdminAccess;

	public int GetSpawnedPropCount( SpawnedProp ignoredProp = null )
	{
		var owner = Network.Owner;
		if ( owner is null || Game.ActiveScene is null )
			return 0;

		return Game.ActiveScene.GetAllComponents<SpawnedProp>()
			.Count( prop => prop.IsValid()
				&& prop != ignoredProp
				&& prop.GameObject.IsValid()
				&& prop.GameObject.Components.Get<Ownable>()?.Owner == owner );
	}

	public void SendPropLimitNotice( SpawnedProp ignoredProp = null )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
			return;

		var limit = HasUnlimitedProps ? "unlimited" : PropLimit.ToString();
		Notices.SendNotice( Network.Owner, "category", Color.Cyan, $"Props: {GetSpawnedPropCount( ignoredProp )}/{limit}", 2 );
	}

	public void SendPropSpawnDeniedNotice( string error )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null || string.IsNullOrWhiteSpace( error ) )
			return;

		if ( _timeSincePropSpawnDeniedNotice < PropSpawnDeniedNoticeCooldownSeconds )
			return;

		_timeSincePropSpawnDeniedNotice = 0;
		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 2 );
	}

	public void MarkPropSpawned()
	{
		if ( !Networking.IsHost )
			return;

		_timeSincePropSpawn = 0;
	}

	public bool CanSpawnProp( out string error )
	{
		error = null;

		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
		{
			error = "Invalid prop spawn request.";
			return false;
		}

		if ( !HasAdminAccess && _timeSincePropSpawn < PropSpawnCooldownSeconds )
		{
			var remaining = MathF.Ceiling( (PropSpawnCooldownSeconds - _timeSincePropSpawn) * 10.0f ) / 10.0f;
			error = $"Slow down before spawning another prop ({remaining:0.0}s).";
			return false;
		}

		if ( HasUnlimitedProps )
			return true;

		if ( GetSpawnedPropCount() < PropLimit )
			return true;

		error = $"Prop limit reached ({PropLimit}/{PropLimit}).";
		return false;
	}
}
