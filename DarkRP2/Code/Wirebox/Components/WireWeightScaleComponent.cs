[Library( "ent_wireweightscale", Title = "Wire Weight Scale" )]
public partial class WireWeightScaleComponent : BaseWireInputOutputComponent
{
	[Sync]
	public float Length { get; set; } = 40;

	public override void WireInitialize()
	{
		this.RegisterInputHandler( "Length", ( float value ) =>
		{
			Length = value;
		}, Length );
	}

	public override PortType[] WireGetOutputs()
	{
		return
		[
			PortType.Float( "Weight" ),
		];
	}

	protected override void OnFixedUpdate()
	{
		if ( !this.IsValid() )
			return;

		var outputs = WirePorts.outputs;
		if ( !outputs.ContainsKey( "Weight" ) )
			return;

		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() || !rb.PhysicsBody.IsValid() )
			return;

		var model = GetComponent<Prop>()?.Model ?? GetComponent<ModelRenderer>()?.Model;
		if ( !model.IsValid() )
			return;

		var measuredWeight = 0f;
		var box = rb.PhysicsBody.GetBounds().Grow( -4 ).Translate( WorldRotation.Up * model.Bounds.Size.z );
		box = box.AddPoint( rb.PhysicsBody.GetBounds().Center + WorldRotation.Up * (Length + model.Bounds.Size.z / 2) );

		foreach ( var ent in Scene.FindInPhysics( box ) )
		{
			if ( ent.GetComponent<Player>() is Player )
			{
				measuredWeight += 100f;
				continue;
			}

			if ( ent.GetComponent<Prop>() is not Prop prop )
				continue;
			if ( prop.GameObject == GameObject )
				continue;
			if ( prop.GetComponent<Rigidbody>()?.PhysicsBody is not PhysicsBody body )
				continue;
			if ( body.Velocity.z > 2 )
				continue;

			measuredWeight += body.Mass * body.GravityScale;
		}

		this.WireTriggerOutput( "Weight", measuredWeight );
	}
}
