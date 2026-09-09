public interface IPhysgunEvent : ISceneEvent<IPhysgunEvent>
{
	public class GrabEvent
	{
		/// <summary>
		/// The connection attempting to grab this object.
		/// </summary>
		public Connection Grabber { get; init; }

		/// <summary>
		/// True when the right-click pull mode is being used.
		/// False when the left-click hold mode is being used.
		/// </summary>
		public bool Pulling { get; init; }

		/// <summary>
		/// Set to true to cancel the grab.
		/// </summary>
		public bool Cancelled { get; set; }
	}

	/// <summary>
	/// Called when a player attempts to grab this object with the physgun.
	/// Set <see cref="GrabEvent.Cancelled"/> to true to reject the grab.
	/// </summary>
	void OnPhysgunGrab( GrabEvent e ) { }
}
