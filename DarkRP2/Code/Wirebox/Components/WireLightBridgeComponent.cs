[Spawnable]
[Library( "ent_wirelightbridge", Title = "Light Bridge" )]
public partial class WireLightBridgeComponent : BaseWireInputComponent
{
	GameObject _bridgeEntity;
	float _length;

	public override void WireInitialize()
	{
		this.RegisterInputHandler( "Length", ( float length ) =>
		{
			length = MathF.Round( length, 1 );
			if ( _length.AlmostEqual( length, 0.5f ) )
				return;

			if ( length < 10f )
			{
				_bridgeEntity?.Destroy();
				_bridgeEntity = null;
				_length = 0f;
				return;
			}

			_length = length;
			UpdateBridge();
		} );
	}

	void UpdateBridge()
	{
		var modelId = WireRectangleMesh.Ensure( (int)_length, 100, 1, 64 );
		if ( !WireRectangleMesh.Models.TryGetValue( modelId, out var model ) || !model.IsValid() )
			return;

		if ( !_bridgeEntity.IsValid() )
		{
			_bridgeEntity = WireRectangleMesh.Spawn( modelId );
		}
		else
		{
			foreach ( var existing in _bridgeEntity.Components.GetAll<FixedJoint>().ToArray() )
				existing.Destroy();

			var bridgeProp = _bridgeEntity.GetComponent<Prop>();
			if ( bridgeProp.IsValid() )
				bridgeProp.Model = model;
		}

		if ( !_bridgeEntity.IsValid() )
			return;

		var bridgeRenderer = _bridgeEntity.GetComponent<ModelRenderer>();
		if ( bridgeRenderer.IsValid() )
		{
			var metal = Material.Load( "materials/wirebox/katlatze/metal.vmat" );
			if ( metal is not null )
				bridgeRenderer.SetMaterialOverride( metal, "" );

			bridgeRenderer.Tint = new Color( 0, 0.35f, 1, 0.7f );
			_bridgeEntity.WorldPosition = Transform.World.PointToWorld( new Vector3( 4, -50, 9.5f ) - bridgeRenderer.Model.PhysicsBounds.Mins );
		}

		_bridgeEntity.WorldRotation = WorldRotation;

		var rb = _bridgeEntity.GetComponent<Rigidbody>();
		if ( rb.IsValid() && rb.Mass < 100f )
			rb.MassOverride = 100f;

		var weld = _bridgeEntity.AddComponent<FixedJoint>();
		weld.Attachment = Joint.AttachmentMode.LocalFrames;
		weld.LocalFrame1 = new Transform();
		weld.LocalFrame2 = GameObject.WorldTransform.WithScale( 1 ).ToLocal( _bridgeEntity.WorldTransform );
		weld.AngularFrequency = 0;
		weld.LinearFrequency = 0;
		weld.Body = GameObject;
		weld.EnableCollision = false;
	}

	protected override void OnDestroy()
	{
		_bridgeEntity?.Destroy();
		base.OnDestroy();
	}
}
