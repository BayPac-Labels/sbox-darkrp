/// <summary>
/// Marks a GameObject as stored in a player's RP pocket (not the hotbar).
/// </summary>
public sealed class PocketedItem : Component
{
	[Sync( SyncFlags.FromHost )]
	public int Slot { get; set; } = -1;

	public string GetDisplayName()
	{
		// Pocketed roots are disabled; always include disabled components.
		if ( GetComponent<MoneyPrinter>( true ) is { IsValid: true } printer )
		{
			var definition = MoneyPrinterDefinition.Get( printer.DefinitionPath );
			return definition?.Title ?? "Printer";
		}

		if ( GetComponent<WeaponShipment>( true ) is { IsValid: true } shipment )
		{
			var title = string.IsNullOrWhiteSpace( shipment.ShipmentTitle ) ? "Shipment" : shipment.ShipmentTitle;
			return $"{title} ({shipment.RemainingWeapons})";
		}

		if ( GetComponent<BaseCarryable>( true ) is { IsValid: true } weapon )
		{
			if ( weapon is SpawnerWeapon spawner && !string.IsNullOrWhiteSpace( spawner.Spawner?.DisplayName ) )
				return spawner.Spawner.DisplayName;

			return weapon.DisplayName ?? "Item";
		}

		return GameObject.Name;
	}

	public string GetIconPath()
	{
		if ( GetComponent<BaseCarryable>( true ) is { IsValid: true } weapon )
			return weapon.InventoryIconOverride ?? weapon.DisplayIcon?.ResourcePath;

		if ( GetComponent<WeaponShipment>( true ) is { IsValid: true } shipment
		     && !string.IsNullOrWhiteSpace( shipment.WeaponPrefabPath ) )
		{
			var prefab = GameObject.GetPrefab( shipment.WeaponPrefabPath );
			var carryable = prefab?.GetComponent<BaseCarryable>( true );
			return carryable?.DisplayIcon?.ResourcePath;
		}

		return null;
	}

	/// <summary>
	/// Material icon used when no texture is available (printers).
	/// </summary>
	public string GetFallbackIcon()
	{
		if ( GetComponent<MoneyPrinter>( true ).IsValid() )
			return "print";

		if ( GetComponent<WeaponShipment>( true ).IsValid() )
			return "inventory_2";

		return "backpack";
	}

	public bool IsWeapon => GetComponent<BaseCarryable>( true ).IsValid();

	public BaseCarryable GetWeapon() => GetComponent<BaseCarryable>( true );
}
