using Sandbox.UI;

public sealed class WeaponShipment : Component, Component.IPressable, IPhysgunEvent
{
	public const string PrefabPath = "entities/shipment/weapon_shipment.prefab";

	static readonly Dictionary<string, BBox> CachedBounds = new();
	const float GhostHeight = 60.0f;
	List<TextRenderer> Labels = new();
	GameObject GhostModel;

	[Property, Sync( SyncFlags.FromHost ), Change( nameof( OnWeaponPrefabPathChanged ) )]
	public string WeaponPrefabPath { get; set; }

	[Property, Sync( SyncFlags.FromHost ), Change( nameof( OnShipmentTitleChanged ) )]
	public string ShipmentTitle { get; set; } = "Weapon Shipment";

	[Property, Sync( SyncFlags.FromHost ), Change( nameof( OnRemainingWeaponsChanged ) )]
	public int RemainingWeapons { get; set; }

	TimeSince _timeSinceDispense;

	protected override void OnStart()
	{
		CacheLabels();
		RefreshLabels();
		RebuildGhostModel();
	}

	protected override void OnUpdate()
	{
		if ( GhostModel.IsValid() )
		{
			GhostModel.LocalRotation *= Rotation.FromYaw( 30.0f * Time.Delta );
		}
	}

	protected override void OnDisabled()
	{
		DestroyGhostModel();
	}

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var title = RemainingWeapons > 0 ? "Dispense Weapon" : "Shipment Empty";
		var icon = RemainingWeapons > 0 ? "inventory_2" : "block";
		var description = $"{ShipmentTitle} - {RemainingWeapons} left";
		return new IPressable.Tooltip( title, icon, description );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		return RemainingWeapons > 0 && !string.IsNullOrWhiteSpace( WeaponPrefabPath );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		TryDispense( e.Source.GameObject );
		return true;
	}

	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( !e.Pulling )
		{
			e.Cancelled = true;
		}
	}

	void OnRemainingWeaponsChanged( int oldCount, int newCount )
	{
		RefreshLabels();

		if ( !Networking.IsHost || newCount > 0 )
			return;

		GameObject.Destroy();
	}

	void OnShipmentTitleChanged( string oldTitle, string newTitle )
	{
		RefreshLabels();
	}

	void OnWeaponPrefabPathChanged( string oldPath, string newPath )
	{
		RebuildGhostModel();
	}

	[Rpc.Host]
	void TryDispense( GameObject presserObject )
	{
		if ( RemainingWeapons <= 0 || string.IsNullOrWhiteSpace( WeaponPrefabPath ) )
			return;

		if ( _timeSinceDispense < 0.2f )
			return;

		var weaponPrefab = GameObject.GetPrefab( WeaponPrefabPath );
		if ( weaponPrefab is null )
			return;

		var owner = GameObject.GetComponent<Ownable>()?.Owner;
		var spawnPosition = WorldTransform.PointToWorld( Vector3.Up * GhostHeight );
		var spawnRotation = Rotation.LookAt( WorldRotation.Forward, Vector3.Up );
		var pickup = weaponPrefab.Clone( new CloneConfig
		{
			Transform = new Transform( spawnPosition, spawnRotation ),
			StartEnabled = true
		} );

		pickup.NetworkSpawn();
		Ownable.Set( pickup, owner );

		if ( pickup.GetComponent<Rigidbody>() is { } rb )
		{
			rb.Velocity = WorldRotation.Forward * 70.0f + Vector3.Up * 35.0f;
			rb.AngularVelocity = Vector3.Random * 7.0f;
		}

		_timeSinceDispense = 0;
		RemainingWeapons = Math.Max( RemainingWeapons - 1, 0 );

		if ( presserObject.IsValid() )
		{
			var player = presserObject.Root.GetComponent<Player>();
			if ( player?.Network.Owner is { } connection )
			{
				Notices.SendNotice( connection, "inventory_2", Color.Green, $"Dispensed from {ShipmentTitle} ({RemainingWeapons} left).", 2 );
			}
		}
	}

	public static bool TrySpawn( Player owner, WeaponShipmentItemDefinition definition )
	{
		if ( !Networking.IsHost || !owner.IsValid() || definition is null )
			return false;

		var shipmentPrefab = GameObject.GetPrefab( PrefabPath );
		if ( shipmentPrefab is null )
			return false;

		var bounds = GetSpawnBounds( shipmentPrefab );

		var eyes = owner.EyeTransform;
		var trace = Game.SceneTrace.Ray( eyes.Position, eyes.Position + eyes.Forward * 200.0f )
			.IgnoreGameObject( owner.GameObject )
			.WithoutTags( "player" )
			.Run();

		var up = trace.Normal.Length.AlmostEqual( 0.0f ) ? Vector3.Up : trace.Normal;
		var backward = -eyes.Forward;
		var right = Vector3.Cross( up, backward ).Normal;
		var forward = Vector3.Cross( right, up ).Normal;

		if ( forward.Length.AlmostEqual( 0.0f ) )
			forward = owner.WorldRotation.Forward.WithZ( 0 ).Normal;

		var spawnTransform = new Transform( trace.EndPosition, Rotation.LookAt( forward, up ) );
		spawnTransform.Position += spawnTransform.Up * -bounds.Mins.z;
		spawnTransform.Rotation *= Rotation.FromYaw( GetYawCorrection( bounds ) );

		var shipmentObject = shipmentPrefab.Clone( new CloneConfig
		{
			Transform = spawnTransform,
			StartEnabled = false
		} );

		var shipment = shipmentObject.GetComponent<WeaponShipment>( true );
		if ( !shipment.IsValid() )
		{
			shipmentObject.Destroy();
			return false;
		}

		shipment.WeaponPrefabPath = definition.WeaponPrefabPath;
		shipment.ShipmentTitle = definition.Title;
		shipment.RemainingWeapons = definition.WeaponsPerShipment;

		shipmentObject.Tags.Add( "removable" );
		Ownable.Set( shipmentObject, owner.Network.Owner );
		shipmentObject.NetworkSpawn();

		if ( shipmentObject.GetComponent<Rigidbody>() is { } rb )
		{
			rb.Velocity = owner.Controller.Velocity;
		}

		return true;
	}

	static BBox GetSpawnBounds( GameObject prefab )
	{
		var resourcePath = prefab.PrefabInstanceSource ?? PrefabPath;
		if ( CachedBounds.TryGetValue( resourcePath, out var bounds ) )
			return bounds;

		bounds = prefab.GetBounds();
		CachedBounds[resourcePath] = bounds;
		return bounds;
	}

	static float GetYawCorrection( BBox bounds )
	{
		var size = bounds.Size;
		return size.x > size.y ? 90.0f : 0.0f;
	}

	void CacheLabels()
	{
		Labels = EnumerateSelfAndDescendants( GameObject )
			.SelectMany( x => x.Components.GetAll<TextRenderer>() )
			.Where( x => x.IsValid() )
			.ToList();
	}

	void RefreshLabels()
	{
		if ( Labels.Count == 0 )
		{
			CacheLabels();
		}

		var labelText = $"{ShipmentTitle} | {RemainingWeapons} LEFT";
		foreach ( var label in Labels )
		{
			if ( !label.IsValid() )
				continue;

			var textScope = label.TextScope;
			if ( textScope.Text == labelText )
				continue;

			textScope.Text = labelText;
			label.TextScope = textScope;
		}
	}

	void RebuildGhostModel()
	{
		DestroyGhostModel();

		if ( string.IsNullOrWhiteSpace( WeaponPrefabPath ) )
			return;

		var weaponPrefab = GameObject.GetPrefab( WeaponPrefabPath );
		var carryable = weaponPrefab?.GetComponent<BaseCarryable>( true );
		if ( !carryable.IsValid() || !carryable.WorldModelPrefab.IsValid() )
			return;

		GhostModel = carryable.WorldModelPrefab.Clone( new CloneConfig
		{
			Parent = GameObject,
			StartEnabled = true,
			Transform = new Transform( Vector3.Up * GhostHeight, Rotation.FromYaw( 180.0f ) )
		} );

		GhostModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		GhostModel.Tags.Add( "worldghost" );
	}

	void DestroyGhostModel()
	{
		GhostModel?.Destroy();
		GhostModel = null;
	}

	static IEnumerable<GameObject> EnumerateSelfAndDescendants( GameObject root )
	{
		yield return root;

		foreach ( var child in root.Children )
		{
			foreach ( var nested in EnumerateSelfAndDescendants( child ) )
			{
				yield return nested;
			}
		}
	}
}
