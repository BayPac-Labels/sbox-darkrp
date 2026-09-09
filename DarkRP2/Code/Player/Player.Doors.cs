using Sandbox.UI;

public sealed partial class Player
{
	const float DoorCommandTraceDistance = 220.0f;
	const float DoorPurchaseHoldDuration = 1.0f;
	const float DoorLockpickHoldDuration = 8.0f;
	const float DoorLockpickAttemptSoundInterval = 2.0f;
	const string DoorLockpickAttemptSoundEvent = "weapons/crowbar/sounds/crowbar.hit.sound";

	RoleplayDoor _doorPurchaseTarget;
	TimeSince _doorPurchaseHoldElapsed;
	bool _isDoorPurchaseHolding;
	bool _hasTriggeredDoorPurchase;
	float _doorPurchaseHoldProgress;
	RoleplayDoor _doorLockpickTarget;
	TimeSince _doorLockpickHoldElapsed;
	bool _isDoorLockpickHolding;
	bool _hasTriggeredDoorLockpick;
	float _doorLockpickHoldProgress;
	TimeSince _timeSinceDoorLockpickAttemptSound;
	RoleplayDoor _doorLockpickBypass;
	TimeSince _doorLockpickBypassTime;

	public bool IsDoorPurchaseHolding => _isDoorPurchaseHolding;
	public float DoorPurchaseHoldProgress => _isDoorPurchaseHolding ? _doorPurchaseHoldProgress : 0.0f;
	public RoleplayDoor DoorPurchaseTarget => _doorPurchaseTarget;
	public bool IsDoorLockpickHolding => _isDoorLockpickHolding;
	public float DoorLockpickHoldProgress => _isDoorLockpickHolding ? _doorLockpickHoldProgress : 0.0f;
	public RoleplayDoor DoorLockpickTarget => _doorLockpickTarget;

	public float GetDoorPurchaseProgress( RoleplayDoor roleplayDoor )
	{
		if ( !_isDoorPurchaseHolding || !roleplayDoor.IsValid() || _doorPurchaseTarget != roleplayDoor )
			return 0.0f;

		return _doorPurchaseHoldProgress;
	}

	public float GetDoorLockpickProgress( RoleplayDoor roleplayDoor )
	{
		if ( !_isDoorLockpickHolding || !roleplayDoor.IsValid() || _doorLockpickTarget != roleplayDoor )
			return 0.0f;

		return _doorLockpickHoldProgress;
	}

	public void SetDoorLockpickBypass( RoleplayDoor roleplayDoor )
	{
		_doorLockpickBypass = roleplayDoor;
		_doorLockpickBypassTime = 0.1f;
	}

	public bool HasDoorLockpickBypass( RoleplayDoor roleplayDoor )
	{
		if ( !roleplayDoor.IsValid() || _doorLockpickBypass != roleplayDoor )
			return false;

		return _doorLockpickBypassTime > 0.0f;
	}

	void HandleDoorUseInput()
	{
		if ( !IsLocalPlayer || !Input.Pressed( "use" ) )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
			return;

		if ( roleplayDoor.CanBePurchased )
			return;

		RequestUseLookedDoor();
		Input.Clear( "use" );
	}

	[ConCmd( "rp_door_buy", ConVarFlags.Server, Help = "Buy the roleplay door you are looking at." )]
	public static void BuyLookedDoorCommand( Connection source )
	{
		var player = FindForConnection( source );
		player?.TryBuyLookedDoor();
	}

	[ConCmd( "rp_door_lock", ConVarFlags.Server, Help = "Lock the roleplay door you own and are looking at." )]
	public static void LockLookedDoorCommand( Connection source )
	{
		var player = FindForConnection( source );
		player?.TrySetLookedDoorLockState( true );
	}

	[ConCmd( "rp_door_unlock", ConVarFlags.Server, Help = "Unlock the roleplay door you own and are looking at." )]
	public static void UnlockLookedDoorCommand( Connection source )
	{
		var player = FindForConnection( source );
		player?.TrySetLookedDoorLockState( false );
	}

	[ConCmd( "rp_door_toggle_lock", ConVarFlags.Server, Help = "Toggle lock state on the roleplay door you own and are looking at." )]
	public static void ToggleLookedDoorLockCommand( Connection source )
	{
		var player = FindForConnection( source );
		player?.TryToggleLookedDoorLock();
	}

	[ConCmd( "rp_door_sell", ConVarFlags.Server, Help = "Sell the roleplay door you own and are looking at." )]
	public static void SellLookedDoorCommand( Connection source )
	{
		var player = FindForConnection( source );
		player?.TrySellLookedDoor();
	}

	[ConCmd( "rp_door_lockpick", ConVarFlags.Server, Help = "Lockpick the roleplay door you are looking at." )]
	public static void LockpickLookedDoorCommand( Connection source )
	{
		var player = FindForConnection( source );
		player?.TryLockpickLookedDoor();
	}

	void HandleDoorPurchaseInput()
	{
		if ( !IsLocalPlayer )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) || !roleplayDoor.CanBePurchased )
		{
			ResetDoorPurchaseHold();
			return;
		}

		if ( !Input.Down( "use" ) )
		{
			if ( _isDoorPurchaseHolding && _doorPurchaseTarget == roleplayDoor && !_hasTriggeredDoorPurchase )
			{
				RequestUseLookedDoor();
				Input.Clear( "use" );
			}

			ResetDoorPurchaseHold();
			return;
		}

		if ( _doorPurchaseTarget != roleplayDoor )
		{
			StartDoorPurchaseHold( roleplayDoor );
		}

		_isDoorPurchaseHolding = true;
		Input.Clear( "use" );

		if ( _hasTriggeredDoorPurchase )
		{
			_doorPurchaseHoldProgress = 1.0f;
			return;
		}

		_doorPurchaseHoldProgress = Math.Clamp( _doorPurchaseHoldElapsed.Relative / DoorPurchaseHoldDuration, 0.0f, 1.0f );

		if ( _doorPurchaseHoldProgress < 1.0f )
			return;

		_doorPurchaseHoldProgress = 1.0f;
		_hasTriggeredDoorPurchase = true;
		RequestBuyLookedDoor();
	}

	public void HandleDoorKeyInput()
	{
		if ( !IsLocalPlayer )
			return;

		if ( Input.Pressed( "attack1" ) )
		{
			RequestSetLookedDoorLockState( true );
			Input.Clear( "attack1" );
			return;
		}

		if ( !Input.Pressed( "attack2" ) )
			return;

		RequestSetLookedDoorLockState( false );
		Input.Clear( "attack2" );
	}

	void HandleDoorSellInput()
	{
		if ( !IsLocalPlayer || !Input.Pressed( "reload" ) )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
			return;

		if ( !roleplayDoor.IsOwnedBy( Network.Owner ) )
			return;

		RequestSellLookedDoor();
		Input.Clear( "reload" );
	}

	void StartDoorPurchaseHold( RoleplayDoor roleplayDoor )
	{
		_doorPurchaseTarget = roleplayDoor;
		_doorPurchaseHoldElapsed = 0;
		_doorPurchaseHoldProgress = 0.0f;
		_hasTriggeredDoorPurchase = false;
		_isDoorPurchaseHolding = true;
	}

	void ResetDoorPurchaseHold()
	{
		_doorPurchaseTarget = null;
		_doorPurchaseHoldProgress = 0.0f;
		_hasTriggeredDoorPurchase = false;
		_isDoorPurchaseHolding = false;
	}

	public void HandleDoorLockpickInput()
	{
		if ( !IsLocalPlayer )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
		{
			ResetDoorLockpickHold();
			return;
		}

		if ( !roleplayDoor.Door.IsLocked && roleplayDoor.CanUseDoor( this ) )
		{
			if ( Input.Pressed( "attack1" ) )
			{
				RequestUseLookedDoor();
				Input.Clear( "attack1" );
			}

			ResetDoorLockpickHold();
			return;
		}

		if ( !roleplayDoor.CanAttemptLockpick( this ) )
		{
			if ( Input.Pressed( "attack1" ) && roleplayDoor.CanUseDoor( this ) )
			{
				RequestUseLookedDoor();
				Input.Clear( "attack1" );
			}

			ResetDoorLockpickHold();
			return;
		}

		if ( !Input.Down( "attack1" ) )
		{
			ResetDoorLockpickHold();
			return;
		}

		if ( _doorLockpickTarget != roleplayDoor )
		{
			StartDoorLockpickHold( roleplayDoor );
		}

		_isDoorLockpickHolding = true;
		Input.Clear( "attack1" );
		PlayDoorLockpickAttemptSoundIfReady();

		if ( _hasTriggeredDoorLockpick )
		{
			_doorLockpickHoldProgress = 1.0f;
			return;
		}

		_doorLockpickHoldProgress = Math.Clamp( _doorLockpickHoldElapsed.Relative / DoorLockpickHoldDuration, 0.0f, 1.0f );

		if ( _doorLockpickHoldProgress < 1.0f )
			return;

		_doorLockpickHoldProgress = 1.0f;
		_hasTriggeredDoorLockpick = true;
		RequestLockpickLookedDoor();
	}

	void StartDoorLockpickHold( RoleplayDoor roleplayDoor )
	{
		ResetDoorLockpickHold();
		_doorLockpickTarget = roleplayDoor;
		_doorLockpickHoldElapsed = 0;
		_doorLockpickHoldProgress = 0.0f;
		_hasTriggeredDoorLockpick = false;
		_isDoorLockpickHolding = true;
		_timeSinceDoorLockpickAttemptSound = DoorLockpickAttemptSoundInterval;
	}

	void ResetDoorLockpickHold()
	{
		_doorLockpickTarget = null;
		_doorLockpickHoldProgress = 0.0f;
		_hasTriggeredDoorLockpick = false;
		_isDoorLockpickHolding = false;
		_timeSinceDoorLockpickAttemptSound = 0;
	}

	void PlayDoorLockpickAttemptSoundIfReady()
	{
		if ( _timeSinceDoorLockpickAttemptSound < DoorLockpickAttemptSoundInterval )
			return;

		_timeSinceDoorLockpickAttemptSound = 0;
		RequestPlayDoorLockpickAttemptSound();
	}


	void TryBuyLookedDoor()
	{
		if ( !Networking.IsHost || Network.Owner is null )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "Look at a roleplay door first.", 3 );
			return;
		}

		if ( roleplayDoor.TryBuy( this, out var error ) )
		{
			var price = Math.Max( 0, roleplayDoor.PurchasePrice );
			PlayDoorActionSound( "sounds/ui/ui.spawn.sound" );
			Notices.SendNotice( Network.Owner, "$", Color.Green, $"Door purchased for ${price:n0}.", 3 );
			return;
		}

		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 3 );
	}

	void TrySetLookedDoorLockState( bool locked )
	{
		if ( !Networking.IsHost || Network.Owner is null )
			return;

		if ( !IsHoldingDoorKeys() )
		{
			Notices.SendNotice( Network.Owner, "key", Color.Red, "Equip your keys first.", 3 );
			return;
		}

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "Look at a roleplay door first.", 3 );
			return;
		}

		if ( roleplayDoor.TrySetLocked( this, locked, out var error ) )
		{
			var message = locked ? "Door locked." : "Door unlocked.";
			var icon = locked ? "lock" : "lock_open";
			Notices.SendNotice( Network.Owner, icon, Color.Green, message, 3 );
			return;
		}

		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 3 );
	}

	void TryToggleLookedDoorLock()
	{
		if ( !Networking.IsHost )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
			return;

		TrySetLookedDoorLockState( !roleplayDoor.Door.IsLocked );
	}

	void TrySellLookedDoor()
	{
		if ( !Networking.IsHost || Network.Owner is null )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "Look at a roleplay door first.", 3 );
			return;
		}

		if ( roleplayDoor.TrySell( this, out var refund, out var error ) )
		{
			PlayDoorActionSound( "sounds/ui/ui.undo.sound" );
			Notices.SendNotice( Network.Owner, "$", Color.Green, $"Door sold for ${refund:n0}.", 3 );
			return;
		}

		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 3 );
	}

	void TryLockpickLookedDoor()
	{
		if ( !Networking.IsHost || Network.Owner is null )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "Look at a roleplay door first.", 3 );
			return;
		}

		if ( roleplayDoor.TryLockpick( this, out var error ) )
		{
			ResetDoorLockpickHold();
			Notices.SendNotice( Network.Owner, "key", Color.Green, "Lockpick succeeded.", 3 );
			return;
		}

		ResetDoorLockpickHold();
		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 3 );
	}

	[Rpc.Host]
	void RequestUseLookedDoor()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
			return;

		if ( !roleplayDoor.CanUseDoor( this ) )
			return;

		roleplayDoor.Door.ToggleFromServer( GameObject );
	}

	[Rpc.Host]
	void RequestBuyLookedDoor()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		TryBuyLookedDoor();
	}

	[Rpc.Host]
	void RequestSetLookedDoorLockState( bool locked )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		TrySetLookedDoorLockState( locked );
	}

	[Rpc.Host]
	void RequestToggleLookedDoorLock()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		TryToggleLookedDoorLock();
	}

	[Rpc.Host]
	void RequestSellLookedDoor()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		TrySellLookedDoor();
	}

	[Rpc.Host]
	public void RequestLockpickLookedDoor()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		TryLockpickLookedDoor();
	}

	[Rpc.Host]
	void RequestPlayDoorLockpickAttemptSound()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		if ( !TryGetLookedRoleplayDoor( out var roleplayDoor ) )
			return;

		roleplayDoor.TryPlayLockpickAttemptSound( this );
	}

	bool TryGetLookedRoleplayDoor( out RoleplayDoor roleplayDoor )
	{
		roleplayDoor = null;

		var trace = Scene.Trace.Ray( EyeTransform.ForwardRay, DoorCommandTraceDistance )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player" )
			.Run();

		if ( !trace.Hit )
			return false;

		roleplayDoor = FindRoleplayDoor( trace.GameObject );
		return roleplayDoor.IsValid();
	}

	static RoleplayDoor FindRoleplayDoor( GameObject gameObject )
	{
		for ( var current = gameObject; current.IsValid(); current = current.Parent )
		{
			var roleplayDoor = current.GetComponent<RoleplayDoor>();
			if ( roleplayDoor.IsValid() )
				return roleplayDoor;
		}

		return null;
	}

	bool IsHoldingDoorKeys()
	{
		var inventory = GetComponent<PlayerInventory>();
		return inventory.IsValid() && inventory.ActiveWeapon is KeyWeapon;
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	void PlayDoorActionSound( string soundEvent )
	{
		if ( string.IsNullOrWhiteSpace( soundEvent ) )
			return;

		Sound.Play( soundEvent );
	}

	[Rpc.Broadcast]
	public void PlayDoorLockpickAttemptSound()
	{
		if ( Application.IsDedicatedServer )
			return;

		var sound = Sound.Play( DoorLockpickAttemptSoundEvent, WorldPosition );
		if ( IsLocalPlayer && sound.IsValid() )
		{
			sound.SpacialBlend = 0;
		}
	}
}
