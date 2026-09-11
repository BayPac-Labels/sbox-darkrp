public sealed class WireCableCleanupSystem : GameObjectSystem<WireCableCleanupSystem>
{
	public WireCableCleanupSystem( Scene scene ) : base( scene )
	{
		WireCable.InitCleanupTimer();
	}
}
