[Icon( "🚪" )]
[Title( "#tool.name.fadingdoor" )]
[ClassName( "fadingdoor" )]
[Group( "#tool.group.tools" )]
public class FadingDoorTool : ToolMode
{
	[Property, Sync, KeyBind, Title( "Fade Key" )]
	public string FadeKey { get; set; } = "5";

	[Property, Sync]
	public bool Toggle { get; set; } = true;

	[Property, Sync, Title( "Start Faded" )]
	public bool StartFaded { get; set; }

	[Property, Sync, Title( "No Effect" )]
	public bool NoEffect { get; set; }

	public override string Description => "#tool.hint.fadingdoor.description";

	public override void OnControl()
	{
		base.OnControl();

		// Keep key capture alive while the spawn menu has this tool's sheet open.
		Sandbox.UI.KeyBindControl.Active?.PollWhileListening();
	}

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.fadingdoor.apply", OnApply );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.fadingdoor.remove", OnRemove );
	}

	void OnApply()
	{
		var select = TraceSelect();
		if ( !select.IsValid() || select.IsWorld || select.IsPlayer )
			return;

		var prop = select.GameObject.GetComponent<Prop>() ?? select.GameObject.GetComponentInChildren<Prop>();
		if ( !prop.IsValid() )
			return;

		ApplyFadingDoor( prop.GameObject, FadeKey, Toggle, StartFaded, NoEffect );
		ShootEffects( select );
	}

	void OnRemove()
	{
		var select = TraceSelect();
		if ( !select.IsValid() || select.IsWorld || select.IsPlayer )
			return;

		RemoveFadingDoor( select.GameObject );
		ShootEffects( select );
	}

	[Rpc.Host]
	private void ApplyFadingDoor( GameObject go, string fadeKey, bool toggle, bool startFaded, bool noEffect )
	{
		if ( !go.IsValid() || go.IsProxy )
			return;
		if ( !CanUseToolOn( go ) )
			return;
		if ( !TryUseToolActionCooldown() )
			return;

		var root = go.Network?.RootGameObject ?? go.Root;
		var door = root.GetComponent<FadingDoor>() ?? root.GetComponentInChildren<FadingDoor>();
		if ( !door.IsValid() )
			door = root.AddComponent<FadingDoor>();

		door.Configure( fadeKey, toggle, startFaded, noEffect );
	}

	[Rpc.Host]
	private void RemoveFadingDoor( GameObject go )
	{
		if ( !go.IsValid() || go.IsProxy )
			return;
		if ( !CanUseToolOn( go ) )
			return;
		if ( !TryUseToolActionCooldown() )
			return;

		var root = go.Network?.RootGameObject ?? go.Root;
		var door = root.GetComponent<FadingDoor>() ?? root.GetComponentInChildren<FadingDoor>();
		if ( !door.IsValid() )
			return;

		if ( door.IsFaded )
			door.SetFaded( false );

		door.Destroy();
	}
}
