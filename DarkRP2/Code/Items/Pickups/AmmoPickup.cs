/// <summary>
/// A pickup that gives the player reserve ammo for a matching weapon.
/// </summary>
public sealed class AmmoPickup : BasePickup
{
	/// <summary>
	/// The ammo resource this pickup gives ammo for.
	/// When set, ammo is added directly to the player's shared pool for that resource.
	/// </summary>
	[Property, Group( "Ammo" )] public AmmoResource AmmoType { get; set; }

	/// <summary>
	/// The quantity of ammo to give.
	/// </summary>
	[Property, Group( "Ammo" )] public int AmmoAmount { get; set; }

	public static bool TrySpawn( Player owner, string prefabPath )
	{
		if ( !Networking.IsHost || !owner.IsValid() || string.IsNullOrWhiteSpace( prefabPath ) )
			return false;

		var prefab = GameObject.GetPrefab( prefabPath );
		var pickup = prefab?.GetComponent<AmmoPickup>( true );
		if ( !pickup.IsValid() || pickup.AmmoType is null || pickup.AmmoAmount <= 0 )
			return false;

		var dropPosition = owner.EyeTransform.Position + owner.EyeTransform.Forward * 48f;
		var dropVelocity = owner.EyeTransform.Forward * 200f + Vector3.Up * 100f;

		var dropped = prefab.Clone( new CloneConfig
		{
			Transform = new Transform( dropPosition ),
			StartEnabled = true
		} );

		dropped.Tags.Add( "removable" );
		dropped.NetworkSpawn();
		Ownable.Set( dropped, owner.Network.Owner );

		if ( dropped.GetComponent<Rigidbody>() is { } rb )
		{
			rb.Velocity = owner.Controller.Velocity + dropVelocity;
			rb.AngularVelocity = Vector3.Random * 8.0f;
		}

		return true;
	}

	public override bool CanPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is not null )
		{
			var ammoInv = player.GetComponent<AmmoInventory>();
			if ( ammoInv is null ) return false;
			return ammoInv.GetAmmo( AmmoType ) < AmmoType.MaxReserve;
		}

		return false;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is not null )
		{
			var ammoInv = player.GetComponent<AmmoInventory>();
			if ( ammoInv is null ) return false;
			return ammoInv.AddAmmo( AmmoType, AmmoAmount ) > 0;
		}

		return true;
	}
}
