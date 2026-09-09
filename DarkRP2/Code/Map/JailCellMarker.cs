namespace Sandbox;

public sealed class JailCellMarker : Component
{
	[Property]
	public bool EnabledForArrests { get; set; } = true;

	public Transform GetCellTransform()
	{
		return GameObject.WorldTransform;
	}
}
