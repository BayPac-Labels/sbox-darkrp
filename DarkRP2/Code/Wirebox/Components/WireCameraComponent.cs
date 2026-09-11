[Library( "ent_wirecamera", Title = "Wire Camera" )]
public partial class WireCameraComponent : BaseWireOutputComponent
{
	SceneCamera _sceneCamera;

	public override PortType[] WireGetOutputs()
	{
		return
		[
			PortType.GameObject( "Self" ),
		];
	}

	public override void WireInitializeOutputs()
	{
		this.WireTriggerOutput( "Self", GameObject );
	}

	public SceneCamera GetSceneCamera()
	{
		_sceneCamera ??= new SceneCamera
		{
			World = Scene.SceneWorld,
			FieldOfView = 90,
			ZFar = 10000,
			ZNear = 1,
		};

		_sceneCamera.Position = WorldPosition + WorldRotation.Up * 2 + WorldRotation.Forward * 2;
		_sceneCamera.Rotation = WorldRotation;
		return _sceneCamera;
	}
}
