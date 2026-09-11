using Sandbox.UI;
using Sandbox.UI.Construct;

[Library( "ent_wiredigitalscreen", Title = "Wire Digital Screen" )]
public partial class WireDigitalScreenComponent : BaseWireInputComponent
{
	[Sync]
	string ValueString { get; set; } = "0";

	[Sync]
	public string LabelPrefix { get; set; } = "Wire Screen: ";

	[Sync]
	Sandbox.WorldPanel WorldPanelComponent { get; set; }

	[Sync]
	GameObject MountPoint { get; set; }

	Sandbox.UI.WorldPanel _worldPanel;
	Label _label;
	Label _value;
	Model _model;

	public static Dictionary<string, ScreenData> ScreenDatabase { get; } = new()
	{
		["models/television/flatscreen_tv.vmdl"] = new ScreenData
		{
			Position = new Vector3( 0, 0, 2 ),
			Rotation = Rotation.From( new Angles( -90, 0, 0 ) ),
			Size = new Vector2( 50, 30 ),
			ScreenTextureIndex = 1,
		},
		["models/others/monitor/monitor.vmdl"] = new ScreenData
		{
			Position = new Vector3( -0.4f, -0.1f, 17.25f ),
			Rotation = Rotation.From( new Angles( -0.1f, 0, 0 ) ),
			Size = new Vector2( 37, 21 ),
			ScreenTextureIndex = 1,
		},
	};

	public override void WireInitialize()
	{
		this.RegisterInputHandler( "Label", ( string value ) =>
		{
			LabelPrefix = value;
		}, LabelPrefix );

		this.RegisterInputHandler( "Text", ( string value ) =>
		{
			ValueString = value;
		}, ValueString );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		_model = GetComponent<ModelRenderer>()?.Model;
		if ( !MountPoint.IsValid() && !IsProxy )
		{
			MountPoint = new GameObject( false, "wire_screen_panel" )
			{
				WorldPosition = WorldPosition,
				Parent = GameObject,
			};
			WorldPanelComponent = MountPoint.AddComponent<Sandbox.WorldPanel>();
			MountPoint.NetworkSpawn( true, null );
		}
	}

	void InitializeRenderScreen()
	{
		if ( !WorldPanelComponent.IsValid() )
			return;

		_worldPanel = WorldPanelComponent.GetPanel() as Sandbox.UI.WorldPanel;
		if ( !_worldPanel.IsValid() )
			return;

		_model ??= GetComponent<ModelRenderer>()?.Model;
		if ( !_model.IsValid() )
			return;

		_label = _worldPanel.Add.Label( "Wire Screen:", "ds-text" );
		_value = _worldPanel.Add.Label( "0", "ds-text" );
		_worldPanel.Style.FlexDirection = FlexDirection.Column;

		_label.Style.Width = _model.Bounds.Size.y * 8.5f;
		_label.Style.TextAlign = TextAlign.Center;
		_label.Style.FontSize = Length.Pixels( _model.Bounds.Size.y * 1.25f );
		_label.Style.FontColor = Color.White;

		_value.Style.Width = _model.Bounds.Size.y * 8.5f;
		_value.Style.TextAlign = TextAlign.Center;
		_value.Style.FontSize = Length.Pixels( _model.Bounds.Size.y );
		_value.Style.FontColor = Color.White;

		var modelData = ScreenDatabase.GetValueOrDefault( _model.Name, new ScreenData
		{
			Position = Vector3.Zero,
			Rotation = Rotation.From( new Angles( -90, 0, 0 ) ),
			Size = new Vector2( 50, 30 ),
			ScreenTextureIndex = 1,
		} );

		if ( MountPoint.IsValid() )
		{
			MountPoint.WorldPosition = Transform.World.PointToWorld( modelData.Position );
			MountPoint.WorldRotation = Transform.World.RotationToWorld( modelData.Rotation );
		}

		WorldPanelComponent.PanelSize = modelData.Size / Sandbox.UI.WorldPanel.ScreenToWorldScale;
	}

	protected override void OnUpdate()
	{
		if ( !WorldPanelComponent.IsValid() || !WorldPanelComponent.GetPanel().IsValid() )
			return;

		if ( !_worldPanel.IsValid() || _label is null || _label.Style.TextAlign != TextAlign.Center )
			InitializeRenderScreen();

		if ( _label is null || _value is null )
			return;

		_label.Text = LabelPrefix;
		_value.Text = ValueString;
	}
}

public struct ScreenData
{
	public Vector3 Position;
	public Rotation Rotation;
	public Vector2 Size;
	public int ScreenTextureIndex;
}
