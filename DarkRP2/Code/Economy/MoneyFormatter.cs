public static class MoneyFormatter
{
	public static string FormatCompact( int amount )
	{
		if ( amount >= 1_000_000_000 )
			return FormatCompact( amount / 1_000_000_000f, "B" );

		if ( amount >= 1_000_000 )
			return FormatCompact( amount / 1_000_000f, "M" );

		if ( amount >= 1_000 )
			return FormatCompact( amount / 1_000f, "K" );

		return amount.ToString();
	}

	static string FormatCompact( float value, string suffix )
	{
		var formatted = value >= 10.0f
			? value.ToString( "0" )
			: value.ToString( "0.#" );

		return $"{formatted}{suffix}";
	}
}
