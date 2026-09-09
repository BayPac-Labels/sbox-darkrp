using Sandbox.Citizen;

public sealed class HandWeapon : MeleeWeapon
{
	private static CitizenAnimationHelper.HoldTypes ResolveHandHoldType()
	{
		if ( Enum.TryParse<CitizenAnimationHelper.HoldTypes>( "Fists", true, out var fists ) )
			return fists;

		if ( Enum.TryParse<CitizenAnimationHelper.HoldTypes>( "Punch", true, out var punch ) )
			return punch;

		return CitizenAnimationHelper.HoldTypes.None;
	}

	protected override bool WantsPrimaryAttack() => Input.Pressed( "attack1" );

	public override void OnAdded( Player player )
	{
		base.OnAdded( player );
		HoldType = ResolveHandHoldType();
	}
}
