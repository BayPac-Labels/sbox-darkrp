/// <summary>
/// Scene-compatible port of wiremod.sbox_tool_auto (attack3 / sbox_tool_auto).
/// Cycles toolgun modes appropriate for the targeted object.
/// </summary>
public static class ToolAuto
{
	static GameObject _lastTarget;
	static int _index = -1;

	[ConCmd( "sbox_tool_auto" )]
	public static void Run( string debug = null )
	{
		var player = Player.FindLocalPlayer();
		if ( !player.IsValid() ) return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		var scene = Game.ActiveScene;
		if ( scene is null ) return;

		var eye = player.EyeTransform;
		var tr = scene.Trace.Ray( eye.Position, eye.Position + eye.Forward * 5000f )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "player", "trigger" )
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() || tr.GameObject.Tags.Has( "world" ) )
			return;

		var target = tr.GameObject;
		var options = CollectTools( target );

		if ( debug == "debug" )
		{
			Log.Info( $"sbox_tool_auto debug: {target.Name} options=[{string.Join( ", ", options )}]" );
			return;
		}

		if ( options.Count == 0 ) return;

		if ( target != _lastTarget )
		{
			_lastTarget = target;
			_index = -1;
		}

		_index = (_index + 1) % options.Count;
		inventory.SetToolMode( options[_index] );
	}

	static List<string> CollectTools( GameObject go )
	{
		var options = new List<string>();

		foreach ( var component in go.Components.GetAll<Component>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !component.IsValid() ) continue;

			var type = Game.TypeLibrary.GetType( component.GetType() );
			var className = type?.ClassName;
			if ( string.IsNullOrWhiteSpace( className ) )
				continue;

			if ( className.StartsWith( "ent_", StringComparison.OrdinalIgnoreCase ) )
			{
				var toolName = className["ent_".Length..];
				if ( HasToolMode( toolName ) && !options.Contains( toolName ) )
					options.Add( toolName );
			}
		}

		if ( go.GetComponent<IWireComponent>() is not null )
		{
			if ( HasToolMode( "wiring" ) && !options.Contains( "wiring" ) )
				options.Add( "wiring" );
			if ( HasToolMode( "wiredebugger" ) && !options.Contains( "wiredebugger" ) )
				options.Add( "wiredebugger" );
		}

		if ( go.GetComponent<Prop>().IsValid() )
		{
			if ( HasToolMode( "weld" ) && !options.Contains( "weld" ) )
				options.Add( "weld" );
		}

		return options;
	}

	static bool HasToolMode( string name )
	{
		return Game.TypeLibrary.GetType<ToolMode>( name ) is not null;
	}
}

public sealed class ToolAutoSystem : GameObjectSystem<ToolAutoSystem>
{
	public ToolAutoSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, OnUpdate, "ToolAuto" );
	}

	void OnUpdate()
	{
		if ( !Game.IsPlaying ) return;
		if ( !Input.Pressed( "attack3" ) ) return;
		if ( Sandbox.DoorRadialMenu.IsOpen ) return;

		var player = Player.FindLocalPlayer();
		if ( !player.IsValid() || !player.IsHoldingToolgun() ) return;
		if ( player.CanOpenDoorRadial( out _ ) ) return;

		var eye = player.EyeTransform;
		var tr = Game.ActiveScene.Trace.Ray( eye.Position, eye.Position + eye.Forward * 5000f )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "player", "trigger" )
			.Run();

		// MMB pockets printers / guns / shipments — don't steal that input for tool cycling.
		if ( tr.Hit && Pocketable.ResolveRoot( tr.GameObject ).IsValid() )
			return;

		ToolAuto.Run();
	}
}
