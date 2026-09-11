using System.Collections.Generic;
using Sandbox;

[Library( "sbox_tool_auto" )]
public static class SboxToolAutoAddon
{
	private static Entity LastTapped;
	private static int CurrentIndex = -1;

	[Event( "game.init" )]
	[Event( "package.mounted" )]
	public static void Initialize()
	{
		if ( !Game.IsClient ) return;
		Log.Info( "Init SboxToolAuto: `sbox_tool_auto` or press attack3 to use" );
	}

	[ConCmd.Client( "sbox_tool_auto" )]
	public static void SboxToolAuto( string param1 = null )
	{
		var ply = Game.LocalPawn as Player;
		if ( ply == null ) return;

		var startPos = ply.EyePosition;
		var tr = Trace.Ray( startPos, startPos + (ply.EyeRotation.Forward * 5000) )
			.WithAnyTags( "solid", "nocollide" )
			.Ignore( ply )
			.Run();

		if ( tr.Entity.IsValid() && !tr.Entity.IsWorld )
		{
			if ( param1 == "debug" )
			{
				Log.Info( $"sbox_tool_auto debug: tr.Entity: {tr.Entity} ClassName: {tr.Entity.ClassName} Name: {tr.Entity.Name} Type: {tr.Entity.GetType()}" );
				foreach ( var property in TypeLibrary.GetPropertyDescriptions( tr.Entity, true ) )
				{
					Log.Info( $"sbox_tool_auto debug: tr.Entity has Property: {property.Name}" );
				}
			}

			var className = tr.Entity.ClassName;
			List<string> toolOptions = new();

			// Entities can define their own custom tools via:
			// public string[] SboxToolAutoTools => new string[] { "tool_constraint" };
			var customTools = TypeLibrary.GetPropertyValue( tr.Entity, "SboxToolAutoTools" );
			if ( customTools is IEnumerable<string> customToolsEnumerable )
			{
				foreach ( var customTool in customToolsEnumerable )
				{
					if ( TypeLibrary.GetType( customTool ) != null )
					{
						toolOptions.Add( customTool );
					}
				}
			}

			// Generic entity -> tool handler
			if ( className.StartsWith( "ent_" ) )
			{
				var possibleTool = className.Replace( "ent_", "tool_" );
				if ( TypeLibrary.GetType( possibleTool ) != null )
				{
					// nice, there's a tool whose ClassName aligns with the entity it spawns (eg. ent_wirebutton + tool_wirebutton)
					toolOptions.Add( possibleTool );
				}
			}

			if ( className == "prop_physics" )
			{
				toolOptions.Add( "tool_constraint" );
				toolOptions.Add( "physgun" );
			}

			// Wirebox compatible entity handler
			if ( TypeLibrary.GetPropertyValue( tr.Entity, "Sandbox.IWireEntity.WirePorts" ) != null )
			{
				toolOptions.Add( "tool_wiring" );
				toolOptions.Add( "tool_debugger" );
			}

			if ( toolOptions.Count == 0 ) return;

			// Always start with the first tool when picking a new Entity
			if ( tr.Entity != LastTapped )
			{
				LastTapped = tr.Entity;
				CurrentIndex = -1;
			}
			CurrentIndex += 1;
			if ( CurrentIndex >= toolOptions.Count ) CurrentIndex = 0;
			var tool = toolOptions[CurrentIndex];

			if ( tool.StartsWith( "tool_" ) )
			{
				ConsoleSystem.Run( "tool_current", tool );
				// weapon_switch is only currently implemented in SandboxPlus, but at least we're loosely coupled in case other Gamemodes want to as well
				ConsoleSystem.Run( "weapon_switch", "weapon_tool" );
			}
			else
			{
				ConsoleSystem.Run( "weapon_switch", tool );
			}
		}
	}

	[GameEvent.Client.BuildInput]
	public static void ProcessClientInput()
	{
		if ( Input.Pressed( "attack3" ) )
		{
			SboxToolAuto();
		}
	}
}
