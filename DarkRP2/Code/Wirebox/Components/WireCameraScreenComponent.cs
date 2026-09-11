[Library( "ent_wirecamerascreen", Title = "Screen - Camera" )]
public partial class WireCameraScreenComponent : BaseWireInputComponent
{
	[Sync]
	WireCameraComponent CameraEntity { get; set; }

	[Sync]
	int Fps { get; set; } = 20;

	Texture _texture;
	SceneCustomObject _renderObject;
	Vector2 _size = new( 500, 300 );
	TimeSince _timeSinceLastRender;

	public override void WireInitialize()
	{
		this.RegisterInputHandler( "Camera", ( GameObject ent ) =>
		{
			if ( !ent.IsValid() || ent.GetComponent<WireCameraComponent>() is not WireCameraComponent cam )
			{
				CameraEntity = null;
				return;
			}

			CameraEntity = cam;
		} );

		this.RegisterInputHandler( "FPS", ( int val ) =>
		{
			Fps = Math.Clamp( val, 1, 60 );
		}, Fps );
	}

	protected override void OnPreRender()
	{
		if ( !CameraEntity.IsValid() || !_texture.IsValid() )
			return;

		if ( _timeSinceLastRender < (1.0f / Fps) )
			return;

		_timeSinceLastRender = 0;
		Graphics.RenderToTexture( CameraEntity.GetSceneCamera(), _texture );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		_renderObject = new SceneCustomObject( Scene.SceneWorld )
		{
			RenderOverride = OnRender,
			RenderingEnabled = false,
		};

		var renderer = GetComponent<ModelRenderer>();
		if ( renderer.IsValid() )
			OnNewModel( renderer.Model );
	}

	public void OnNewModel( Model model )
	{
		var sceneObject = GetComponent<ModelRenderer>()?.SceneObject;
		if ( !sceneObject.IsValid() || !model.IsValid() )
			return;

		var modelData = WireDigitalScreenComponent.ScreenDatabase.GetValueOrDefault( model.Name, new ScreenData { Size = new Vector2( 50, 50 ) } );
		_size = modelData.Size / Sandbox.UI.WorldPanel.ScreenToWorldScale;

		_texture?.Dispose();
		_texture = Texture.CreateRenderTarget()
			.WithSize( _size )
			.WithScreenFormat()
			.WithDynamicUsage()
			.Create();

		Scene.Camera?.RenderToTexture( _texture );

		sceneObject.Batchable = false;
		if ( sceneObject.Attributes.GetTexture( "screen" ) != null )
		{
			sceneObject.Attributes.Set( "screen", _texture );
			return;
		}

		var material = Material.Create( "wire_camerascreen_rendertexture", "simple" );
		material.Set( "Color", _texture );
		material.Set( "Normal", Texture.Transparent );

		var mats = model.Materials.ToList();
		for ( var i = 0; i < mats.Count; i++ )
			mats[i].Attributes.Set( "materialIndex" + i, 1 );

		var renderer = GetComponent<ModelRenderer>();
		renderer.SetMaterialOverride( material, "materialIndex" + modelData.ScreenTextureIndex );
		sceneObject.SetMaterialOverride( material, "materialIndex" + modelData.ScreenTextureIndex, 1 );
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		_renderObject?.Delete();
		_renderObject = null;
		_texture?.Dispose();
	}

	void OnRender( SceneObject sceneObject )
	{
		Graphics.RenderTarget = RenderTarget.From( _texture );
		Graphics.Attributes.SetCombo( "D_WORLDPANEL", 0 );
		Graphics.Viewport = new Rect( 0, _size );
		Graphics.Clear();
		Graphics.RenderTarget = null;
	}
}
