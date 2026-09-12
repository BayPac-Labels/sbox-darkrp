using Sandbox.UI;

/// <summary>
/// 18-slot RP pocket inventory (printers, guns, shipments). Separate from the hotbar.
/// </summary>
public sealed class PlayerPocket : Component, Local.IPlayerEvents
{
	public const int DefaultMaxSlots = 18;

	[Property] public int MaxSlots { get; set; } = DefaultMaxSlots;

	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// Host-authoritative slot → networked object id. Lives on the player (pre-spawn) so clients
	/// always know pocket contents even when late-added <see cref="PocketedItem"/> Sync fails.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public NetDictionary<string, Guid> SlotObjectIds { get; set; } = new();

	public IEnumerable<PocketedItem> Items
	{
		get
		{
			for ( var i = 0; i < MaxSlots; i++ )
			{
				var item = GetSlot( i );
				if ( item.IsValid() )
					yield return item;
			}
		}
	}

	public PocketedItem GetSlot( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots )
			return null;

		if ( TryGetSlotObject( slot, out var go ) )
		{
			var pocketed = go.GetComponent<PocketedItem>( true );
			if ( pocketed.IsValid() )
			{
				// Late-added PocketedItem Sync often doesn't pair; keep local index correct for UI.
				if ( pocketed.Slot != slot )
					pocketed.Slot = slot;

				return pocketed;
			}
		}

		// Host fallback while migrating / before SlotObjectIds is written.
		foreach ( var item in GetComponentsInChildren<PocketedItem>( true ) )
		{
			if ( item.IsValid() && item.Slot == slot )
				return item;
		}

		return null;
	}

	public int FindEmptySlot()
	{
		for ( var i = 0; i < MaxSlots; i++ )
		{
			if ( !GetSlot( i ).IsValid() )
				return i;
		}

		return -1;
	}

	public bool IsFull => FindEmptySlot() < 0;

	bool TryGetSlotObject( int slot, out GameObject go )
	{
		go = null;
		if ( SlotObjectIds is null )
			return false;

		var key = SlotKey( slot );
		if ( !SlotObjectIds.TryGetValue( key, out var id ) || id == Guid.Empty )
			return false;

		go = Scene.Directory.FindByGuid( id );
		return go.IsValid();
	}

	static string SlotKey( int slot ) => slot.ToString();

	void BindSlot( int slot, GameObject root )
	{
		if ( SlotObjectIds is null )
			SlotObjectIds = new NetDictionary<string, Guid>();

		SlotObjectIds[SlotKey( slot )] = root.IsValid() ? root.Id : Guid.Empty;
	}

	void UnbindSlot( int slot )
	{
		SlotObjectIds?.Remove( SlotKey( slot ) );
	}

	void ClearSlotBinding( GameObject root )
	{
		if ( !root.IsValid() || SlotObjectIds is null )
			return;

		var id = root.Id;
		foreach ( var pair in SlotObjectIds.ToList() )
		{
			if ( pair.Value == id )
				SlotObjectIds.Remove( pair.Key );
		}
	}

	static void RefreshNetworked( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		go.Network?.Refresh();
	}

	const float WorldPickupTraceDistance = 220.0f;

	/// <summary>
	/// Middle-mouse world → pocket. Local player only; host validates via look-trace.
	/// </summary>
	public void HandleWorldPickupInput()
	{
		if ( !Player.IsValid() || !Player.IsLocalPlayer )
			return;

		if ( !Input.Pressed( "attack3" ) )
			return;

		if ( Player.IsSpawnOrInspectMenuOpen() )
			return;

		if ( DoorRadialMenu.IsOpen )
			return;

		// Client-side gate so we don't steal MMB when looking at nothing pocketable.
		if ( !TraceLookedPocketable().IsValid() )
			return;

		Input.Clear( "attack3" );
		RequestPickupLooked();
	}

	void RequestPickupLooked()
	{
		if ( !Networking.IsHost )
		{
			HostPickupLooked();
			return;
		}

		PickupLooked();
	}

	[Rpc.Host]
	void HostPickupLooked()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		PickupLooked();
	}

	void PickupLooked()
	{
		var root = TraceLookedPocketable();
		if ( !root.IsValid() )
			return;

		TryPickup( root );
	}

	GameObject TraceLookedPocketable()
	{
		var trace = Scene.Trace.Ray( Player.EyeTransform.ForwardRay, WorldPickupTraceDistance )
			.IgnoreGameObjectHierarchy( Player.GameObject )
			.WithoutTags( "player" )
			.Run();

		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return null;

		return Pocketable.ResolveRoot( trace.GameObject );
	}

	/// <summary>
	/// True when <paramref name="root"/> is a shipment that can fully merge into an existing pocket stack.
	/// </summary>
	public bool CanMergeShipment( GameObject root )
	{
		var incoming = root?.GetComponent<WeaponShipment>( true );
		if ( !incoming.IsValid() )
			return false;

		if ( incoming.RemainingWeapons <= 0 || incoming.RemainingWeapons > WeaponShipment.MaxStackSize )
			return false;

		foreach ( var item in Items )
		{
			var existing = item.GetComponent<WeaponShipment>( true );
			if ( existing.IsValid() && incoming.CanStackWith( existing ) )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Pocket a world entity (MMB pickup). Host-authoritative.
	/// </summary>
	public bool TryPickup( GameObject target )
	{
		if ( !Networking.IsHost )
			return false;

		var root = Pocketable.ResolveRoot( target );
		if ( !root.IsValid() )
			return false;

		if ( root.GetComponent<PocketedItem>() is { IsValid: true } alreadyPocketed && alreadyPocketed.Slot >= 0 )
			return false;

		if ( !Pocketable.CanPlayerAccess( Player, root ) )
		{
			NotifyOwner( "block", Color.Red, "You can't pick that up.", 2 );
			return false;
		}

		// Already in this player's hotbar — use MoveFromHotbar instead.
		if ( root.GetComponent<BaseCarryable>( true ) is { IsValid: true } hotbarWeapon
		     && hotbarWeapon.Owner == Player
		     && !root.GetComponent<PocketedItem>().IsValid()
		     && hotbarWeapon.InventorySlot >= 0 )
		{
			return false;
		}

		var description = Describe( root );
		if ( TryMergeWorldShipment( root ) )
		{
			NotifyOwner( "inventory_2", Color.Green, $"Stacked {description}.", 2 );
			return true;
		}

		var slot = FindEmptySlot();
		if ( slot < 0 )
		{
			NotifyOwner( "block", Color.Red, "Pocket full.", 2 );
			return false;
		}

		Stash( root, slot );
		NotifyOwner( "inventory_2", Color.Green, $"Pocketed {description}.", 2 );
		return true;
	}

	/// <summary>
	/// Drop a pocket slot into the world in front of the player.
	/// </summary>
	public bool Drop( int slot )
	{
		if ( !Networking.IsHost )
		{
			HostDrop( slot );
			return true;
		}

		var item = GetSlot( slot );
		if ( !item.IsValid() )
			return false;

		var go = item.GameObject;
		var name = item.GetDisplayName();

		UnbindSlot( slot );
		item.Destroy();

		PlaceInWorld( go );
		NotifyOwner( "inventory_2", Color.White, $"Dropped {name}.", 2 );
		return true;
	}

	[Rpc.Host]
	void HostDrop( int slot )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		Drop( slot );
	}

	/// <summary>
	/// Split <paramref name="amount"/> weapons from a pocketed shipment into a new pocket slot.
	/// </summary>
	public bool SplitShipment( int slot, int amount )
	{
		if ( !Networking.IsHost )
		{
			HostSplitShipment( slot, amount );
			return true;
		}

		if ( amount < 1 )
			return false;

		var item = GetSlot( slot );
		if ( !item.IsValid() )
			return false;

		var source = item.GetComponent<WeaponShipment>( true );
		if ( !source.IsValid() )
			return false;

		if ( amount >= source.RemainingWeapons )
			return false;

		if ( amount > WeaponShipment.MaxStackSize )
			return false;

		if ( string.IsNullOrWhiteSpace( source.WeaponPrefabPath ) )
			return false;

		var emptySlot = FindEmptySlot();
		if ( emptySlot < 0 )
		{
			NotifyOwner( "block", Color.Red, "Pocket full.", 2 );
			return false;
		}

		var shipmentPrefab = GameObject.GetPrefab( WeaponShipment.PrefabPath );
		if ( shipmentPrefab is null )
			return false;

		var splitObject = shipmentPrefab.Clone( new CloneConfig
		{
			Transform = global::Transform.Zero,
			StartEnabled = false
		} );

		var split = splitObject.GetComponent<WeaponShipment>( true );
		if ( !split.IsValid() )
		{
			splitObject.Destroy();
			return false;
		}

		split.WeaponPrefabPath = source.WeaponPrefabPath;
		split.ShipmentTitle = source.ShipmentTitle;
		split.RemainingWeapons = amount;

		splitObject.Tags.Add( "removable" );
		Ownable.Set( splitObject, Player.Network.Owner );
		splitObject.NetworkSpawn();

		Stash( splitObject, emptySlot );
		source.RemainingWeapons -= amount;

		NotifyOwner( "inventory_2", Color.Green, $"Split {amount} from {source.ShipmentTitle}.", 2 );
		return true;
	}

	[Rpc.Host]
	void HostSplitShipment( int slot, int amount )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		SplitShipment( slot, amount );
	}

	public void MoveSlot( int fromSlot, int toSlot )
	{
		if ( !Networking.IsHost )
		{
			HostMoveSlot( fromSlot, toSlot );
			return;
		}

		if ( fromSlot == toSlot )
			return;

		if ( fromSlot < 0 || fromSlot >= MaxSlots || toSlot < 0 || toSlot >= MaxSlots )
			return;

		var from = GetSlot( fromSlot );
		if ( !from.IsValid() )
			return;

		var to = GetSlot( toSlot );
		if ( to.IsValid() && TryMergePocketShipments( from, to ) )
			return;

		var fromObject = from.GameObject;
		var toObject = to.IsValid() ? to.GameObject : null;

		from.Slot = toSlot;
		BindSlot( toSlot, fromObject );

		if ( to.IsValid() )
		{
			to.Slot = fromSlot;
			BindSlot( fromSlot, toObject );
		}
		else
		{
			UnbindSlot( fromSlot );
		}
	}

	/// <summary>
	/// Merge two pocketed shipments of the same weapon when their combined count is at most <see cref="WeaponShipment.MaxStackSize"/>.
	/// </summary>
	bool TryMergePocketShipments( PocketedItem sourceItem, PocketedItem destItem )
	{
		var source = sourceItem.GetComponent<WeaponShipment>( true );
		var dest = destItem.GetComponent<WeaponShipment>( true );
		if ( !source.IsValid() || !dest.IsValid() )
			return false;

		if ( !source.CanStackWith( dest ) )
			return false;

		dest.RemainingWeapons += source.RemainingWeapons;

		var sourceObject = sourceItem.GameObject;
		var sourceSlot = sourceItem.Slot;
		UnbindSlot( sourceSlot );
		ClearSlotBinding( sourceObject );
		sourceItem.Destroy();
		sourceObject.Destroy();
		RefreshNetworked( GameObject );

		NotifyOwner( "inventory_2", Color.Green, $"Stacked {dest.ShipmentTitle} ({dest.RemainingWeapons}).", 2 );
		return true;
	}

	/// <summary>
	/// Absorb a world shipment into an existing pocket stack when the combined count is at most max.
	/// </summary>
	bool TryMergeWorldShipment( GameObject root )
	{
		if ( !CanMergeShipment( root ) )
			return false;

		var incoming = root.GetComponent<WeaponShipment>( true );

		foreach ( var item in Items )
		{
			var existing = item.GetComponent<WeaponShipment>( true );
			if ( !existing.IsValid() || !incoming.CanStackWith( existing ) )
				continue;

			existing.RemainingWeapons += incoming.RemainingWeapons;
			root.Destroy();
			return true;
		}

		return false;
	}

	[Rpc.Host]
	void HostMoveSlot( int fromSlot, int toSlot )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		MoveSlot( fromSlot, toSlot );
	}

	/// <summary>
	/// Hotbar → pocket while holding C.
	/// </summary>
	public bool MoveFromHotbar( int hotbarSlot, int pocketSlot )
	{
		if ( !Networking.IsHost )
		{
			HostMoveFromHotbar( hotbarSlot, pocketSlot );
			return true;
		}

		var inventory = Player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() )
			return false;

		if ( pocketSlot < 0 || pocketSlot >= MaxSlots )
			return false;

		var weapon = inventory.GetSlot( hotbarSlot );
		if ( !Pocketable.IsRpGun( weapon ) )
			return false;

		var occupying = GetSlot( pocketSlot );
		if ( occupying.IsValid() )
		{
			if ( !occupying.IsWeapon || !Pocketable.IsRpGun( occupying.GetWeapon() ) )
				return false;
		}

		if ( inventory.ActiveWeapon == weapon )
			inventory.SwitchWeapon( null, true );

		// Detach hotbar gun first so the pocket gun can take its slot.
		weapon.InventorySlot = -1;
		weapon.GameObject.Enabled = false;

		if ( occupying.IsValid() )
		{
			var otherWeapon = occupying.GetWeapon();
			UnbindSlot( pocketSlot );
			ClearSlotBinding( occupying.GameObject );
			occupying.Destroy();
			if ( !EquipWeaponOnHotbar( otherWeapon, hotbarSlot ) )
			{
				// Roll back: put original weapon back on the hotbar.
				EquipWeaponOnHotbar( weapon, hotbarSlot );
				return false;
			}
		}

		var pocketed = weapon.GameObject.GetOrAddComponent<PocketedItem>();
		pocketed.Slot = pocketSlot;
		BindSlot( pocketSlot, weapon.GameObject );
		RefreshNetworked( weapon.GameObject );
		RefreshNetworked( GameObject );
		return true;
	}

	[Rpc.Host]
	void HostMoveFromHotbar( int hotbarSlot, int pocketSlot )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		MoveFromHotbar( hotbarSlot, pocketSlot );
	}

	/// <summary>
	/// Pocket → hotbar while holding C. Printers / shipments stay in the pocket.
	/// </summary>
	public bool MoveToHotbar( int pocketSlot, int hotbarSlot )
	{
		if ( !Networking.IsHost )
		{
			HostMoveToHotbar( pocketSlot, hotbarSlot );
			return true;
		}

		var inventory = Player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() )
			return false;

		var item = GetSlot( pocketSlot );
		if ( !item.IsValid() || !item.IsWeapon )
			return false;

		var weapon = item.GetWeapon();
		if ( !Pocketable.IsRpGun( weapon ) )
			return false;

		if ( hotbarSlot < 0 || hotbarSlot >= inventory.MaxSlots )
			return false;

		var occupying = inventory.GetSlot( hotbarSlot );
		if ( occupying.IsValid() )
		{
			if ( !Pocketable.IsRpGun( occupying ) )
				return false;
		}

		// Free the pocket slot before optionally swapping the hotbar gun into it.
		UnbindSlot( pocketSlot );
		item.Destroy();

		if ( occupying.IsValid() )
		{
			if ( inventory.ActiveWeapon == occupying )
				inventory.SwitchWeapon( null, true );

			occupying.InventorySlot = -1;
			occupying.GameObject.Enabled = false;
			var swapPocketed = occupying.GameObject.GetOrAddComponent<PocketedItem>();
			swapPocketed.Slot = pocketSlot;
			BindSlot( pocketSlot, occupying.GameObject );
			RefreshNetworked( occupying.GameObject );
		}

		var equipped = EquipWeaponOnHotbar( weapon, hotbarSlot );
		RefreshNetworked( weapon.GameObject );
		RefreshNetworked( GameObject );
		return equipped;
	}

	[Rpc.Host]
	void HostMoveToHotbar( int pocketSlot, int hotbarSlot )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		MoveToHotbar( pocketSlot, hotbarSlot );
	}

	bool EquipWeaponOnHotbar( BaseCarryable weapon, int hotbarSlot )
	{
		if ( !weapon.IsValid() )
			return false;

		ClearSlotBinding( weapon.GameObject );

		if ( weapon.GameObject.GetComponent<PocketedItem>() is { IsValid: true } leftover )
			leftover.Destroy();

		weapon.InventorySlot = hotbarSlot;
		weapon.GameObject.SetParent( Player.GameObject, false );
		weapon.LocalTransform = global::Transform.Zero;
		weapon.GameObject.Enabled = false;
		weapon.SetDropped( false );
		weapon.OnAdded( Player );

		if ( Network.Owner is not null )
			weapon.Network.AssignOwnership( Network.Owner );
		else
			weapon.Network.DropOwnership();

		return true;
	}

	void Stash( GameObject root, int slot )
	{
		if ( root.GetComponent<WeaponShipment>( true ) is { IsValid: true } shipment
		     && shipment.RemainingWeapons > WeaponShipment.MaxStackSize )
		{
			shipment.RemainingWeapons = WeaponShipment.MaxStackSize;
		}

		// World weapons: strip DroppedWeapon pressable while pocketed.
		if ( root.GetComponent<DroppedWeapon>() is { IsValid: true } dropped )
			dropped.Enabled = false;

		if ( root.GetComponent<BaseCarryable>( true ) is { IsValid: true } weapon )
		{
			weapon.InventorySlot = -1;
			weapon.SetDropped( false );

			if ( Network.Owner is not null )
				weapon.Network.AssignOwnership( Network.Owner );
			else
				weapon.Network.DropOwnership();
		}

		UndoSystem.Current?.Remove( root );

		root.SetParent( GameObject, false );
		root.LocalTransform = global::Transform.Zero;
		root.Enabled = false;

		if ( Network.Owner is not null )
			root.Network.AssignOwnership( Network.Owner );

		var pocketed = root.GetOrAddComponent<PocketedItem>();
		pocketed.Slot = slot;
		BindSlot( slot, root );

		// Hierarchy / Enabled / late PocketedItem must be refreshed for connected clients.
		RefreshNetworked( root );
		RefreshNetworked( GameObject );
	}

	void PlaceInWorld( GameObject go )
	{
		var dropPosition = Player.EyeTransform.Position + Player.EyeTransform.Forward * 48f;
		var dropVelocity = Player.EyeTransform.Forward * 200f + Vector3.Up * 100f;

		ClearSlotBinding( go );
		if ( go.GetComponent<PocketedItem>() is { IsValid: true } pocketed )
			pocketed.Destroy();

		go.SetParent( null, true );
		go.WorldPosition = dropPosition;
		go.WorldRotation = Rotation.LookAt( Player.EyeTransform.Forward.WithZ( 0 ).Normal, Vector3.Up );
		go.Enabled = true;

		Ownable.Set( go, Player.Network.Owner );
		go.Tags.Add( "removable" );

		if ( go.GetComponent<DroppedWeapon>() is { IsValid: true } dropped )
			dropped.Enabled = true;

		if ( go.GetComponent<BaseCarryable>( true ) is { IsValid: true } weapon )
			weapon.SetDropped( true );

		if ( go.GetComponent<Rigidbody>() is { IsValid: true } rb )
		{
			rb.Velocity = Player.Controller.Velocity + dropVelocity;
			rb.AngularVelocity = Vector3.Random * 8.0f;
		}

		RefreshNetworked( go );
		RefreshNetworked( GameObject );
	}

	static string Describe( GameObject go )
	{
		if ( go.GetComponent<PocketedItem>() is { IsValid: true } pocketed )
			return pocketed.GetDisplayName();

		if ( go.GetComponent<MoneyPrinter>( true ) is { IsValid: true } printer )
			return MoneyPrinterDefinition.Get( printer.DefinitionPath )?.Title ?? "Printer";

		if ( go.GetComponent<WeaponShipment>( true ) is { IsValid: true } shipment )
			return shipment.ShipmentTitle;

		if ( go.GetComponent<BaseCarryable>( true ) is { IsValid: true } weapon )
			return weapon.DisplayName;

		return go.Name;
	}

	void NotifyOwner( string icon, Color color, string text, float seconds )
	{
		if ( Player.Network.Owner is { } owner )
			Notices.SendNotice( owner, icon, color, text, seconds );
	}

	void Local.IPlayerEvents.OnDied( PlayerDiedParams args )
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var item in Items.ToList() )
		{
			if ( !item.IsValid() )
				continue;

			var slot = item.Slot;
			Drop( slot );
		}
	}
}
