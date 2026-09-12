/// <summary>
/// Compile-time <see cref="Cloud.Model"/> references so wire tools that default to
/// workshop models ship those assets (same pattern as upstream Wirebox).
/// </summary>
static class WireCloudModels
{
	internal static void EnsureBundled()
	{
		Bundle();
	}

	static void Bundle()
	{
		_ = Cloud.Model( "baik.flatscreen_tv" );
		_ = Cloud.Model( "eurorp.monitor" );
		_ = Cloud.Model( "facepunch.pallet" );
		_ = Cloud.Model( "smlp.camera" );
		_ = Cloud.Model( "wiremod.gps" );
		_ = Cloud.Model( "wiremod.keyboard" );
		_ = Cloud.Model( "wiremod.numberpad" );
	}

	internal static string CloudIdentFor( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		if ( Matches( path, "wiremod.keyboard", "wiremod/keyboard", "models/wirebox/seal_enthusiast/keyboard/keyboard.vmdl" ) )
			return "wiremod.keyboard";

		if ( Matches( path, "wiremod.numberpad", "wiremod/numberpad" ) )
			return "wiremod.numberpad";

		if ( Matches( path, "wiremod.gps", "wiremod/gps", "models/wirebox/seal_enthusiast/gps/gps.vmdl" ) )
			return "wiremod.gps";

		if ( Matches( path, "smlp.camera", "smlp/camera", "camera/camera.vmdl" ) )
			return "smlp.camera";

		if ( Matches( path, "baik.flatscreen_tv", "baik/flatscreen_tv", "models/television/flatscreen_tv.vmdl" ) )
			return "baik.flatscreen_tv";

		if ( Matches( path, "eurorp.monitor", "eurorp/monitor", "models/others/monitor/monitor.vmdl" ) )
			return "eurorp.monitor";

		if ( Matches( path, "facepunch.pallet", "facepunch/pallet", "models/sbox_props/pallet/pallet.vmdl" ) )
			return "facepunch.pallet";

		if ( !path.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase )
			&& !path.EndsWith( ".vmdl_c", StringComparison.OrdinalIgnoreCase ) )
			return path;

		return null;
	}

	internal static Model TryResolve( string path )
	{
		var ident = CloudIdentFor( path );
		if ( ident is null )
			return null;

		if ( Matches( ident, "wiremod.keyboard" ) )
			return Take( Cloud.Model( "wiremod.keyboard" ) );

		if ( Matches( ident, "wiremod.numberpad" ) )
			return Take( Cloud.Model( "wiremod.numberpad" ) );

		if ( Matches( ident, "wiremod.gps" ) )
			return Take( Cloud.Model( "wiremod.gps" ) );

		if ( Matches( ident, "smlp.camera" ) )
			return Take( Cloud.Model( "smlp.camera" ) );

		if ( Matches( ident, "baik.flatscreen_tv" ) )
			return Take( Cloud.Model( "baik.flatscreen_tv" ) );

		if ( Matches( ident, "eurorp.monitor" ) )
			return Take( Cloud.Model( "eurorp.monitor" ) );

		if ( Matches( ident, "facepunch.pallet" ) )
			return Take( Cloud.Model( "facepunch.pallet" ) );

		return null;
	}

	static Model Take( Model model ) => model.IsValid() && !model.IsError ? model : null;

	static bool Matches( string path, params string[] names )
	{
		foreach ( var name in names )
		{
			if ( path.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}
}
