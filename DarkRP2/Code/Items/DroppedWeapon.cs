using Sandbox.UI;

public sealed class DroppedWeapon : Component, Component.IPressable, PlayerController.IEvents
{
	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return null;

		var name = weapon.DisplayName.ToUpper();

		if ( HasInput() ) return new IPressable.Tooltip( "Can't pick this up", "block", name );

		if ( !IsInventoryFull() )
		{
			var description = CanPocket( e ) ? $"{name} · MMB Pocket" : name;
			return new IPressable.Tooltip( "Pick up", "inventory_2", description );
		}

		if ( CanPocket( e ) )
			return new IPressable.Tooltip( "Pocket", "inventory_2", "Press MMB to pocket" );

		return new IPressable.Tooltip( "Inventory Full", "block", name );
	}

	private bool IsInventoryFull()
	{
		var player = Player.FindLocalPlayer();
		if ( !player.IsValid() ) return false;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return false;

		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return false;

		return !inventory.CanTake( weapon );
	}

	private bool HasInput()
	{
		var weapon = GetComponent<BaseWeapon>();
		if ( !weapon.IsValid() ) return false;
		return weapon.ShootInput.IsEnabled || weapon.SecondaryInput.IsEnabled;
	}

	bool CanPocket( IPressable.Event e )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !Pocketable.IsRpGun( weapon ) )
			return false;

		var player = e.Source.GameObject.Root.GetComponent<Player>() ?? Player.FindLocalPlayer();
		if ( !player.IsValid() )
			return false;

		var pocket = player.GetComponent<PlayerPocket>();
		if ( !pocket.IsValid() || pocket.IsFull )
			return false;

		return Pocketable.CanPlayerAccess( player, GameObject );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		if ( HasInput() ) return false;

		// E is Use — equip to hotbar only. Pocketing is middle-mouse.
		return !IsInventoryFull();
	}

	bool IPressable.Press( IPressable.Event e )
	{
		DoPickup( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	private void DoPickup( GameObject presserObject )
	{
		if ( !presserObject.IsValid() ) return;

		var player = presserObject.Root.GetComponent<Player>();
		if ( !player.IsValid() ) return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		var weapon = GetComponent<BaseCarryable>();
		if ( weapon.IsValid() && inventory.CanTake( weapon ) )
		{
			TakeIntoInventory( inventory );
			return;
		}

		ShowInventoryFull();
	}

	/// <summary>
	/// Disables world-physics components and moves the weapon into the player's inventory.
	/// </summary>
	private void TakeIntoInventory( PlayerInventory inventory )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return;

		// seems to fix missing audio on shipment weapons
		var prefabSource = weapon.GameObject.PrefabInstanceSource;
		if ( !string.IsNullOrWhiteSpace( prefabSource ) && inventory.Pickup( prefabSource ) )
		{
			weapon.DestroyGameObject();
			return;
		}

		if ( !inventory.Take( weapon, true ) )
		{
			ShowInventoryFull();
			return;
		}

		Enabled = false;
	}

	[Rpc.Owner]
	private void ShowInventoryFull()
	{
		Notices.AddNotice( "block", Color.Red, "Inventory Full", 2 );
	}
}
