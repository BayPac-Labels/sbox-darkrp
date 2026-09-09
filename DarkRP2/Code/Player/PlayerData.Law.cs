public sealed partial class PlayerData
{
	[Sync] public bool IsWanted { get; set; }
	[Sync] public string WantedReason { get; set; }
	[Sync] public bool IsArrested { get; set; }
	[Sync] public float ArrestTimeRemaining { get; set; }

	void TickLawState()
	{
		if ( !IsArrested )
			return;

		ArrestTimeRemaining = Math.Max( 0.0f, ArrestTimeRemaining - Time.Delta );
		if ( ArrestTimeRemaining > 0.0f )
			return;

		var player = Player.For( PlayerId );
		player?.ReleaseFromArrest();
	}
}
