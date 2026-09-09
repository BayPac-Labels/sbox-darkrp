public sealed partial class Player : IPhysgunEvent
{
	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		var grabber = Player.FindForConnection( e.Grabber );
		var hasAccess = e.Grabber?.IsHost == true || grabber?.HasAdminAccess == true;

		if ( !hasAccess )
		{
			e.Cancelled = true;
		}
	}
}
