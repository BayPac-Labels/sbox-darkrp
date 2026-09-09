public sealed class RagebaitProjectile : Component, Component.ICollisionListener
{
	[Property] public GameObject Attacker { get; set; }
	[Property] public float Damage { get; set; } = 8.0f;
	[Property] public float ImpactForce { get; set; } = 450.0f;
	[Property] public float Lifetime { get; set; } = 8.0f;

	TimeSince TimeSinceSpawned { get; set; }
	bool HasImpacted { get; set; }

	protected override void OnEnabled()
	{
		TimeSinceSpawned = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( TimeSinceSpawned >= Lifetime )
			GameObject.Destroy();
	}

	void Component.ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost || HasImpacted || GameObject.IsDestroyed )
			return;

		var other = collision.Other.GameObject;
		if ( !other.IsValid() )
			return;

		if ( Attacker.IsValid() && other.Root == Attacker.Root )
			return;

		HasImpacted = true;
		ApplyImpact( other, collision.Contact.Point, collision.Contact.Normal );
		PlayImpactEffects( collision.Contact.Point, collision.Contact.Normal );
		GameObject.Destroy();
	}

	void ApplyImpact( GameObject target, Vector3 position, Vector3 normal )
	{
		var damageable = target.GetComponentInParent<IDamageable>();
		if ( damageable is not null )
		{
			var damageInfo = new DamageInfo( Damage, Attacker.IsValid() ? Attacker : GameObject, GameObject )
			{
				Position = position,
				Origin = WorldPosition
			};

			damageable.OnDamage( damageInfo );
		}

		if ( target.GetComponentInChildren<Rigidbody>() is { } rb && rb.IsValid() )
		{
			var direction = (WorldPosition - position).Normal;
			if ( direction.Length.AlmostEqual( 0.0f ) )
				direction = -normal;

			rb.ApplyImpulse( direction * ImpactForce );
		}
	}

	[Rpc.Broadcast]
	void PlayImpactEffects( Vector3 position, Vector3 normal )
	{
		if ( Application.IsDedicatedServer )
			return;

		var effectPrefab = GameObject.GetPrefab( "entities/particles/fx_vomit.prefab" );
		if ( effectPrefab.IsValid() )
		{
			var effectPosition = position + Vector3.Down * 14.0f;
			var effect = effectPrefab.Clone( new CloneConfig
			{
				Transform = new Transform( effectPosition, Rotation.Identity, 0.35f ),
				StartEnabled = true
			} );

			var temporaryEffect = effect.GetOrAddComponent<TemporaryEffect>();
			temporaryEffect.DestroyAfterSeconds = 5.0f;
			temporaryEffect.WaitForChildEffects = false;
		}
	}
}
