public sealed class MoneyStack : Component, Component.IPressable, IPhysgunEvent
{
	public const string PrefabPath = "entities/money/money_stack.prefab";
	List<TextRenderer> Labels = new();

	[Property, Sync( SyncFlags.FromHost ), Change( nameof( OnAmountChanged ) )]
	public int Amount { get; set; } = 100;

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( Amount <= 0 )
			return null;

		return new IPressable.Tooltip( "Pick up", "$", $"${Amount:n0}" );
	}

	bool IPressable.CanPress( IPressable.Event e ) => Amount > 0;

	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( !e.Pulling )
		{
			e.Cancelled = true;
		}
	}

	bool IPressable.Press( IPressable.Event e )
	{
		DoPickup( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void DoPickup( GameObject presserObject )
	{
		if ( GameObject.IsDestroyed || Amount <= 0 )
			return;

		if ( !presserObject.IsValid() )
			return;

		var player = presserObject.Root.GetComponent<Player>();
		if ( !player.IsValid() )
			return;

		player.GiveMoney( Amount );
		PlayPickupEffects();
		GameObject.Destroy();
	}

	[Rpc.Broadcast]
	void PlayPickupEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		Sound.Play( "sounds/ui/ui.spawn.sound", WorldPosition );
	}

	protected override void OnStart()
	{
		CacheLabels();
		RefreshLabels();
	}

	protected override void OnUpdate()
	{
		if ( Labels.Count == 0 )
		{
			CacheLabels();
		}

		RefreshLabels();
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
		var labelText = $"${MoneyFormatter.FormatCompact( Amount )}";
		var enabled = Amount > 0;

		foreach ( var label in Labels )
		{
			if ( !label.IsValid() )
				continue;

			label.Enabled = enabled;

			var textScope = label.TextScope;
			if ( textScope.Text == labelText )
				continue;

			textScope.Text = labelText;
			label.TextScope = textScope;
		}
	}

	void OnAmountChanged( int oldAmount, int newAmount )
	{
		RefreshLabels();
	}

	public static bool TrySpawn( Player owner, int amount )
	{
		if ( !Networking.IsHost || !owner.IsValid() || amount <= 0 )
			return false;

		var prefab = GameObject.GetPrefab( PrefabPath );
		if ( prefab is null )
		{
			Log.Warning( $"Prefab not found: {PrefabPath}" );
			return false;
		}

		var dropDirection = owner.EyeTransform.Forward.WithZ( 0 ).Normal;
		if ( dropDirection.Length.AlmostEqual( 0.0f ) )
			dropDirection = owner.WorldTransform.Rotation.Forward.WithZ( 0 ).Normal;

		var dropPosition = owner.EyeTransform.Position + dropDirection * 42.0f + Vector3.Down * 24.0f;
		var dropRotation = Rotation.LookAt( dropDirection == Vector3.Zero ? Vector3.Forward : dropDirection, Vector3.Up );

		var dropped = prefab.Clone( new CloneConfig
		{
			Transform = new Transform( dropPosition, dropRotation ),
			StartEnabled = true
		} );

		var moneyStack = dropped.GetComponent<MoneyStack>( true );
		if ( !moneyStack.IsValid() )
		{
			dropped.Destroy();
			return false;
		}

		moneyStack.Amount = amount;
		moneyStack.CacheLabels();
		moneyStack.RefreshLabels();
		dropped.NetworkSpawn();
		Ownable.Set( dropped, owner.Network.Owner );

		if ( dropped.GetComponent<Rigidbody>() is { } rb )
		{
			rb.Velocity = owner.Controller.Velocity + dropDirection * 120.0f + Vector3.Up * 60.0f;
			rb.AngularVelocity = Vector3.Random * 4.0f;
		}

		return true;
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
