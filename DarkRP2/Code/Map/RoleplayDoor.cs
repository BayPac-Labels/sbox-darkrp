using Facepunch;
using Sandbox.UI;
using System.Text.Json.Serialization;

public sealed class RoleplayDoor : Component
{
	const string GovernmentJobCategory = "Government";
	const float LockpickCooldownSeconds = 3.0f;
	public const bool DefaultAllowGovernmentLockpick = true;
	public const int MaxOwnedPerPlayer = 6;
	public const int MaxAllowedUsers = 8;
	public const int MaxTitleLength = 32;

	[RequireComponent] public Door Door { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public int PurchasePrice { get; set; } = 500;

	[Property, Sync( SyncFlags.FromHost )]
	public bool IsGovernment { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public bool IsPublic { get; set; }

	[Property, Sync( SyncFlags.FromHost ), Group( "Group" )]
	public string DoorGroupId { get; set; }

	[Property, Sync( SyncFlags.FromHost ), Group( "Group" )]
	public string DoorGroupTitle { get; set; }

	[Property, Sync( SyncFlags.FromHost ), Group( "Lockpick" )]
	public bool AllowGovernmentLockpick { get; set; } = DefaultAllowGovernmentLockpick;

	[Property, Group( "Sound" )]
	public SoundEvent LockSound { get; set; } = new( "entities/door/sounds/door_lock.sound" );

	[Property, Group( "Sound" )]
	public SoundEvent UnlockSound { get; set; } = new( "entities/door/sounds/door_unlock.sound" );

	[Sync( SyncFlags.FromHost )]
	private Guid _ownerId { get; set; }

	[Sync( SyncFlags.FromHost )]
	private string _allowedUserIds { get; set; } = string.Empty;

	/// <summary>Owner-set title for tooltips and the radial hub. Cleared on sell.</summary>
	[Sync( SyncFlags.FromHost )]
	private string _customTitle { get; set; } = string.Empty;

	private TimeUntil _lockpickCooldown;

	[Property, ReadOnly, JsonIgnore]
	public Connection Owner
	{
		get => Connection.All.FirstOrDefault( x => x.Id == _ownerId );
		private set => _ownerId = value?.Id ?? Guid.Empty;
	}

	public Guid OwnerId => _ownerId;
	public bool IsOwned => _ownerId != Guid.Empty;
	public string CustomTitle => _customTitle ?? string.Empty;
	public bool CanBePurchased => !IsGovernment && !IsPublic && GetGroupDoors().All( door => !door.IsOwned && !door.IsGovernment && !door.IsPublic );
	bool CanLockpickGovernmentDoor => AllowGovernmentLockpick || DefaultAllowGovernmentLockpick;

	protected override void OnStart()
	{
		if ( !Networking.IsHost || !Door.IsValid() )
			return;

		if ( IsGovernment )
		{
			Door.IsLocked = true;
			return;
		}

		if ( IsPublic )
		{
			Door.IsLocked = false;
		}
	}

	public bool IsOwnedBy( Connection connection )
	{
		if ( connection is null )
			return false;

		return _ownerId == connection.Id;
	}

	public bool IsAllowedUser( Connection connection )
	{
		if ( connection is null )
			return false;

		return GetAllowedUserIds().Contains( connection.Id );
	}

	public bool CanManageUsers( Player player )
	{
		if ( !player.IsValid() || IsGovernment || IsPublic )
			return false;

		if ( IsOwnedBy( player.Network.Owner ) )
			return true;

		return player.IsLocalPlayer && Connection.Local is not null && _ownerId == Connection.Local.Id;
	}

	public static int CountOwnedBy( Connection owner )
	{
		if ( owner is null || Game.ActiveScene is null )
			return 0;

		return Game.ActiveScene.GetAllComponents<RoleplayDoor>()
			.Count( door => door.IsValid() && door.IsOwnedBy( owner ) );
	}

	public IReadOnlyList<RoleplayDoor> GetGroupDoors()
	{
		if ( Game.ActiveScene is null )
			return new[] { this };

		var groupId = NormalizeGroupId( DoorGroupId );
		if ( string.IsNullOrEmpty( groupId ) )
			return new[] { this };

		return Game.ActiveScene.GetAllComponents<RoleplayDoor>()
			.Where( door => door.IsValid() && string.Equals( NormalizeGroupId( door.DoorGroupId ), groupId, StringComparison.OrdinalIgnoreCase ) )
			.OrderBy( door => door.GameObject.Id )
			.ToList();
	}

	public int GetGroupPurchasePrice()
	{
		var group = GetGroupDoors();
		return Math.Max( 0, group[0].PurchasePrice );
	}

	public IReadOnlyList<Guid> GetAllowedUserIds()
	{
		if ( string.IsNullOrWhiteSpace( _allowedUserIds ) )
			return Array.Empty<Guid>();

		var ids = new List<Guid>();
		foreach ( var part in _allowedUserIds.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( Guid.TryParse( part, out var id ) && id != Guid.Empty && !ids.Contains( id ) )
				ids.Add( id );
		}

		return ids;
	}

	public IReadOnlyList<(Guid Id, string Name)> GetAllowedUserEntries()
	{
		return GetAllowedUserIds()
			.Select( id => (Id: id, Name: ResolveUserName( id )) )
			.ToList();
	}

	public IReadOnlyList<Connection> GetAddableUsers()
	{
		var allowed = GetAllowedUserIds();
		return Connection.All
			.Where( connection => connection is not null && connection.Id != _ownerId && !allowed.Contains( connection.Id ) )
			.OrderBy( connection => connection.DisplayName )
			.ToList();
	}

	public bool CanPress( IPressable.Event e, Door.DoorState state )
	{
		if ( state is not (Door.DoorState.Open or Door.DoorState.Closed) )
			return false;

		var player = GetPlayer( e );
		if ( !player.IsValid() )
			return false;

		if ( CanBePurchased )
			return true;

		if ( CanUseDoor( player ) )
			return true;

		return CanAttemptLockpick( player );
	}

	public bool Press( IPressable.Event e, Door.DoorState state )
	{
		if ( !CanPress( e, state ) )
			return false;

		var player = GetPlayer( e );
		if ( !player.IsValid() )
			return false;

		if ( CanBePurchased )
			return false;

		if ( !CanUseDoor( player ) )
			return false;

		Door.Toggle( e.Source.GameObject );
		return true;
	}

	public bool TryBuy( Player buyer, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !buyer.IsValid() )
		{
			error = "Invalid door purchase request.";
			return false;
		}

		if ( IsGovernment || GetGroupDoors().Any( door => door.IsGovernment ) )
		{
			error = "Government doors can't be bought.";
			return false;
		}

		if ( IsPublic || GetGroupDoors().Any( door => door.IsPublic ) )
		{
			error = "Public doors can't be bought.";
			return false;
		}

		var group = GetGroupDoors();
		if ( group.Any( door => door.IsOwned ) )
		{
			error = "This door is already owned.";
			return false;
		}

		if ( CountOwnedBy( buyer.Network.Owner ) + group.Count > MaxOwnedPerPlayer )
		{
			error = $"You already own {MaxOwnedPerPlayer} doors.";
			return false;
		}

		var price = GetGroupPurchasePrice();
		if ( !buyer.TryTakeMoney( price ) )
		{
			error = "You don't have enough money.";
			return false;
		}

		foreach ( var door in group )
		{
			door.Owner = buyer.Network.Owner;
			door.WriteAllowedUserIds( Array.Empty<Guid>() );
			if ( door.Door.IsValid() )
			{
				door.Door.IsLocked = false;
			}
		}

		return true;
	}

	public bool TrySell( Player seller, out int refund, out string error )
	{
		refund = 0;
		error = null;

		if ( !Networking.IsHost || !seller.IsValid() )
		{
			error = "Invalid door sale request.";
			return false;
		}

		if ( IsGovernment )
		{
			error = "Government doors can't be sold.";
			return false;
		}

		if ( IsPublic )
		{
			error = "Public doors can't be sold.";
			return false;
		}

		if ( !IsOwnedBy( seller.Network.Owner ) )
		{
			error = IsOwned ? "Only the door owner can sell it." : "Buy this door first.";
			return false;
		}

		refund = GetGroupPurchasePrice();
		ClearGroupOwnership( seller.GameObject );

		if ( refund > 0 )
		{
			seller.GiveMoney( refund );
		}

		return true;
	}

	public bool TrySetCustomTitle( Player actor, string title, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !actor.IsValid() )
		{
			error = "Invalid door rename request.";
			return false;
		}

		if ( !CanManageUsers( actor ) )
		{
			error = "Only the door owner can rename it.";
			return false;
		}

		var cleaned = SanitizeTitle( title );
		if ( string.IsNullOrWhiteSpace( cleaned ) )
		{
			error = "Enter a door name.";
			return false;
		}

		foreach ( var door in GetGroupDoors() )
		{
			door._customTitle = cleaned;
		}

		return true;
	}

	public bool TrySetLocked( Player actor, bool locked, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !actor.IsValid() )
		{
			error = "Invalid door lock request.";
			return false;
		}

		if ( IsPublic )
		{
			error = "Public doors can't be locked.";
			return false;
		}

		if ( IsGovernment )
		{
			if ( !CanAccessGovernmentDoor( actor ) )
			{
				error = "Only government jobs can do that.";
				return false;
			}
		}
		else if ( !CanControlLock( actor ) )
		{
			error = IsOwned ? "You don't have keys for this door." : "Buy this door first.";
			return false;
		}

		if ( Door.IsLocked == locked )
		{
			error = locked ? "Door is already locked." : "Door is already unlocked.";
			return false;
		}

		if ( locked && Door.State is Door.DoorState.Open or Door.DoorState.Opening )
		{
			Door.CloseFromServer( actor.GameObject );
		}

		Door.IsLocked = locked;

		var sound = locked ? LockSound : UnlockSound;
		if ( sound is not null )
		{
			PlayLockSound( sound );
		}

		return true;
	}

	public bool TryAddUser( Player actor, Guid userId, out Connection added, out string error )
	{
		added = null;
		error = null;

		if ( !Networking.IsHost || !actor.IsValid() )
		{
			error = "Invalid door user request.";
			return false;
		}

		if ( !CanManageUsers( actor ) )
		{
			error = "Only the door owner can add users.";
			return false;
		}

		if ( userId == Guid.Empty || userId == _ownerId )
		{
			error = "You can't add that player.";
			return false;
		}

		added = Connection.All.FirstOrDefault( connection => connection.Id == userId );
		if ( added is null )
		{
			error = "That player is not online.";
			return false;
		}

		var users = GetAllowedUserIds().ToList();
		if ( users.Contains( userId ) )
		{
			error = "That player already has access.";
			return false;
		}

		if ( users.Count >= MaxAllowedUsers )
		{
			error = $"This door already has {MaxAllowedUsers} users.";
			return false;
		}

		users.Add( userId );
		foreach ( var door in GetGroupDoors() )
		{
			door.WriteAllowedUserIds( users );
		}

		return true;
	}

	public bool TryRemoveUser( Player actor, Guid userId, out Connection removed, out string error )
	{
		removed = null;
		error = null;

		if ( !Networking.IsHost || !actor.IsValid() )
		{
			error = "Invalid door user request.";
			return false;
		}

		if ( !CanManageUsers( actor ) )
		{
			error = "Only the door owner can kick users.";
			return false;
		}

		if ( userId == Guid.Empty || !GetAllowedUserIds().Contains( userId ) )
		{
			error = "That player does not have access.";
			return false;
		}

		removed = Connection.All.FirstOrDefault( connection => connection.Id == userId );
		RemoveAllowedUserFromGroup( userId );
		return true;
	}

	public bool TryLockpick( Player actor, out string error )
	{
		error = null;

		if ( !CanAttemptLockpick( actor, out error ) )
			return false;

		if ( _lockpickCooldown > 0.0f )
		{
			error = "Lockpick is cooling down.";
			return false;
		}

		Door.IsLocked = false;
		actor.SetDoorLockpickBypass( this );

		if ( Door.State is Door.DoorState.Closed or Door.DoorState.Closing )
		{
			Door.OpenFromServer( actor.GameObject );
		}
		else if ( Door.State is Door.DoorState.Open or Door.DoorState.Opening )
		{
			Door.CloseFromServer( actor.GameObject );
		}

		_lockpickCooldown = LockpickCooldownSeconds;

		if ( UnlockSound is not null )
		{
			PlayLockSound( UnlockSound );
		}

		if ( Owner is { } ownerConnection && ownerConnection != actor.Network.Owner )
		{
			Notices.SendNotice( ownerConnection, "warning", Color.Orange, $"{actor.DisplayName} lockpicked your door.", 3 );
		}

		return true;
	}

	public bool CanAttemptLockpick( Player actor )
	{
		return CanAttemptLockpick( actor, out _ );
	}

	public bool CanAttemptLockpick( Player actor, out string error )
	{
		error = null;

		if ( !actor.IsValid() )
		{
			error = "Invalid lockpick request.";
			return false;
		}

		if ( !actor.IsThief )
		{
			error = "Only the thief can use lockpick.";
			return false;
		}

		if ( IsPublic )
		{
			error = "Public doors can't be lockpicked.";
			return false;
		}

		if ( IsGovernment )
		{
			if ( !CanLockpickGovernmentDoor )
			{
				error = "Government doors can't be lockpicked.";
				return false;
			}

			return true;
		}

		if ( !IsOwned )
		{
			error = "Only owned doors can be lockpicked.";
			return false;
		}

		if ( IsOwnedBy( actor.Network.Owner ) )
		{
			error = "This is your own door.";
			return false;
		}

		if ( IsAllowedUser( actor.Network.Owner ) )
		{
			error = "You already have access to this door.";
			return false;
		}

		return true;
	}

	public IPressable.Tooltip BuildTooltip( Player player, Door.DoorState state )
	{
		var isOwner = player.IsValid() && IsOwnedBy( player.Network.Owner );
		var isAllowed = player.IsValid() && IsAllowedUser( player.Network.Owner );
		var title = Door.IsLocked ? "Locked" : state == Door.DoorState.Open ? "Close" : "Open";
		var icon = Door.IsLocked ? "lock" : "door_front";
		var groupLabel = GetGroupDisplayName();

		if ( IsPublic )
		{
			title = state == Door.DoorState.Open ? "Close" : "Open";
			return new IPressable.Tooltip( title, "door_front", "Public door" );
		}

		if ( IsGovernment )
		{
			if ( player.IsValid() && player.IsThief && CanLockpickGovernmentDoor && Door.IsLocked )
			{
				return new IPressable.Tooltip( "Lockpick", "key", "Hold attack" );
			}

			if ( CanAccessGovernmentDoor( player ) )
			{
				return new IPressable.Tooltip( title, icon, "Use keys" );
			}

			return new IPressable.Tooltip( "Government Door", "lock", "Government only" );
		}

		if ( !IsOwned )
		{
			var group = GetGroupDoors();
			var price = GetGroupPurchasePrice();
			var buyTitle = !string.IsNullOrWhiteSpace( groupLabel )
				? groupLabel
				: group.Count > 1 ? $"Buy {group.Count} Doors" : "Buy Door";
			var progress = player.IsValid() ? player.GetDoorPurchaseProgress( this ) : 0.0f;
			var description = $"E to open, Hold E to buy {price:n0}$";

			if ( progress > 0.0f )
			{
				var percent = (int)MathF.Round( progress * 100.0f );
				description = $"{BuildProgressBar( progress )} {percent}%";
			}

			return new IPressable.Tooltip( buyTitle, "$", description );
		}

		if ( player.IsValid() && player.IsThief && !isOwner && !isAllowed )
		{
			if ( Door.IsLocked || player.IsDoorLockpickHolding && player.DoorLockpickTarget == this )
			{
				return new IPressable.Tooltip( "Lockpick", "key", "Hold attack" );
			}
		}

		if ( isOwner )
		{
			if ( !string.IsNullOrWhiteSpace( groupLabel ) )
				title = $"{title}\n{groupLabel}";

			return new IPressable.Tooltip( title, icon, "Press MMB to Manage" );
		}

		if ( isAllowed )
		{
			if ( !string.IsNullOrWhiteSpace( groupLabel ) )
				title = $"{title}\n{groupLabel}";

			return new IPressable.Tooltip( title, icon, "Use keys" );
		}

		var ownerName = Owner?.DisplayName ?? "Unknown";
		var lockState = Door.IsLocked ? "locked" : "unlocked";
		return new IPressable.Tooltip( title, icon, $"{ownerName} - {lockState}" );
	}

	public bool CanControlLock( Player player )
	{
		if ( !player.IsValid() )
			return false;

		if ( IsGovernment )
			return CanAccessGovernmentDoor( player );

		if ( IsPublic )
			return false;

		return IsOwnedBy( player.Network.Owner ) || IsAllowedUser( player.Network.Owner );
	}

	public bool CanUseDoor( Player player )
	{
		if ( !player.IsValid() )
			return false;

		if ( Door.IsLocked )
		{
			if ( IsPublic )
				return true;

			if ( IsGovernment )
				return false;

			return IsOwnedBy( player.Network.Owner ) || IsAllowedUser( player.Network.Owner );
		}

		if ( IsPublic )
			return true;

		if ( IsGovernment )
			return CanAccessGovernmentDoor( player );

		return true;
	}

	public bool TryPlayLockpickAttemptSound( Player actor )
	{
		if ( !Networking.IsHost || !CanAttemptLockpick( actor ) )
			return false;

		if ( actor.IsValid() )
		{
			actor.PlayDoorLockpickAttemptSound();
		}

		return true;
	}

	public static void ReleaseOwnershipFor( Connection connection, out int refund )
	{
		refund = 0;

		if ( !Networking.IsHost || connection is null || Game.ActiveScene is null )
			return;

		var owned = Game.ActiveScene.GetAllComponents<RoleplayDoor>()
			.Where( door => door.IsValid() && door.IsOwnedBy( connection ) )
			.ToList();

		var seen = new HashSet<RoleplayDoor>();
		foreach ( var door in owned )
		{
			if ( !seen.Add( door ) )
				continue;

			foreach ( var grouped in door.GetGroupDoors() )
			{
				seen.Add( grouped );
			}

			refund += door.GetGroupPurchasePrice();
			door.ClearGroupOwnership( null );
		}

		foreach ( var door in Game.ActiveScene.GetAllComponents<RoleplayDoor>() )
		{
			if ( !door.IsValid() || !door.IsAllowedUser( connection ) )
				continue;

			door.RemoveAllowedUserFromGroup( connection.Id );
		}
	}

	void ClearGroupOwnership( GameObject closer )
	{
		foreach ( var door in GetGroupDoors() )
		{
			door.CloseAndUnlock( closer );
			door.WriteAllowedUserIds( Array.Empty<Guid>() );
			door._customTitle = string.Empty;
			door.Owner = null;
		}
	}

	void CloseAndUnlock( GameObject closer )
	{
		if ( !Door.IsValid() )
			return;

		if ( Door.State is Door.DoorState.Open or Door.DoorState.Opening )
		{
			Door.CloseFromServer( closer );
		}

		Door.IsLocked = false;
	}

	void RemoveAllowedUserFromGroup( Guid userId )
	{
		var users = GetAllowedUserIds().Where( id => id != userId ).ToList();
		foreach ( var door in GetGroupDoors() )
		{
			door.WriteAllowedUserIds( users );
		}
	}

	void WriteAllowedUserIds( IEnumerable<Guid> ids )
	{
		_allowedUserIds = string.Join( ",", ids.Where( id => id != Guid.Empty ).Distinct() );
	}

	public string GetHubTitle()
	{
		if ( !string.IsNullOrWhiteSpace( _customTitle ) )
			return _customTitle.Trim();

		if ( !string.IsNullOrWhiteSpace( DoorGroupTitle ) )
			return DoorGroupTitle.Trim();

		var count = GetGroupDoors().Count;
		return count > 1 ? $"{count} Doors" : "Door";
	}

	string GetGroupDisplayName()
	{
		if ( !string.IsNullOrWhiteSpace( _customTitle ) )
			return _customTitle.Trim();

		if ( !string.IsNullOrWhiteSpace( DoorGroupTitle ) )
			return DoorGroupTitle.Trim();

		var count = GetGroupDoors().Count;
		return count > 1 ? $"{count} doors" : null;
	}

	static string SanitizeTitle( string title )
	{
		if ( string.IsNullOrWhiteSpace( title ) )
			return string.Empty;

		var cleaned = title.Trim().Replace( '\n', ' ' ).Replace( '\r', ' ' );
		while ( cleaned.Contains( "  " ) )
			cleaned = cleaned.Replace( "  ", " " );

		if ( cleaned.Length > MaxTitleLength )
			cleaned = cleaned[..MaxTitleLength].TrimEnd();

		return cleaned;
	}

	static string ResolveUserName( Guid userId )
	{
		var data = PlayerData.For( userId );
		if ( data.IsValid() && !string.IsNullOrWhiteSpace( data.DisplayName ) )
			return data.DisplayName;

		return Connection.All.FirstOrDefault( connection => connection.Id == userId )?.DisplayName ?? "Unknown";
	}

	static string NormalizeGroupId( string groupId )
	{
		return string.IsNullOrWhiteSpace( groupId ) ? string.Empty : groupId.Trim();
	}

	static Player GetPlayer( IPressable.Event e )
	{
		if ( !e.Source.IsValid() )
			return null;

		return e.Source.GameObject.Root.GetComponent<Player>();
	}

	static bool CanAccessGovernmentDoor( Player player )
	{
		if ( !player.IsValid() )
			return false;

		var category = player.CurrentJobDefinition?.Category?.Trim();
		return string.Equals( category, GovernmentJobCategory, StringComparison.OrdinalIgnoreCase );
	}

	[Rpc.Broadcast]
	void PlayLockSound( SoundEvent sound )
	{
		if ( sound is null )
			return;

		GameObject.PlaySound( sound );
	}

	static string BuildProgressBar( float progress )
	{
		const int segments = 10;
		var clamped = Math.Clamp( progress, 0.0f, 1.0f );
		var filled = (int)MathF.Round( clamped * segments );
		return $"[{new string( '#', filled )}{new string( '-', segments - filled )}]";
	}
}
