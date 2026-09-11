using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// Minimal rectangle mesh builder for wire light bridges.
/// Adapted from SandboxPlus VertexMeshBuilder (MIT).
/// </summary>
public static class WireRectangleMesh
{
	public static Dictionary<string, Model> Models { get; } = new();

	public static string Ensure( float length, float width, float height, int texSize = 64 )
	{
		var size = new Vector3( length, width, height );
		var key = $"wire_rect_{size.x}_{size.y}_{size.z}_{texSize}";
		if ( Models.ContainsKey( key ) )
			return key;

		var mins = size * -0.5f;
		var maxs = size * 0.5f;
		var vertices = new List<MeshVertex>();
		AddRectangle( vertices, Vector3.Zero, size, texSize, Color.White );

		var mesh = new Mesh( Material.Load( "materials/default/vertex_color.vmat" ) );
		mesh.CreateVertexBuffer<MeshVertex>( vertices.Count, MeshVertex.Layout, vertices.ToArray() );
		mesh.Bounds = new BBox( mins, maxs );

		var indices = new List<int>( vertices.Count );
		for ( var i = 0; i < vertices.Count; i++ )
			indices.Add( i );
		mesh.CreateIndexBuffer( indices.Count, indices.ToArray() );

		var modelBuilder = new ModelBuilder();
		modelBuilder.AddMesh( mesh );
		var box = new BBox( mins, maxs );
		modelBuilder.AddCollisionBox( box.Size * 0.5f, box.Center );
		modelBuilder.WithMass( box.Size.x * box.Size.y * box.Size.z * 0.001f );

		Models[key] = modelBuilder.Create();
		return key;
	}

	public static GameObject Spawn( string modelId )
	{
		if ( !Models.TryGetValue( modelId, out var model ) || !model.IsValid() )
			return null;

		var go = new GameObject( false, "wire_bridge" );
		go.Tags.Add( "solid" );
		go.Tags.Add( "removable" );

		var prop = go.AddComponent<Prop>();
		prop.Model = model;
		prop.Health = 0;

		var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
		if ( rb.Mass < 100f )
			rb.MassOverride = 100f;

		go.NetworkSpawn( true, null );
		return go;
	}

	static void AddRectangle( List<MeshVertex> vertices, Vector3 position, Vector3 size, int texSize, Color color )
	{
		var rot = Rotation.Identity;
		var f = size.x * rot.Forward * 0.5f;
		var l = size.y * rot.Left * 0.5f;
		var u = size.z * rot.Up * 0.5f;

		CreateQuad( vertices, new Ray( position + f, f.Normal ), l, u, texSize, color );
		CreateQuad( vertices, new Ray( position - f, -f.Normal ), l, -u, texSize, color );
		CreateQuad( vertices, new Ray( position + l, l.Normal ), -f, u, texSize, color );
		CreateQuad( vertices, new Ray( position - l, -l.Normal ), f, u, texSize, color );
		CreateQuad( vertices, new Ray( position + u, u.Normal ), f, l, texSize, color );
		CreateQuad( vertices, new Ray( position - u, -u.Normal ), f, -l, texSize, color );
	}

	static void CreateQuad( List<MeshVertex> vertices, Ray origin, Vector3 width, Vector3 height, int texSize, Color color )
	{
		var normal = origin.Forward;
		var tangent = width.Normal;

		var a = new MeshVertex( origin.Position - width - height, normal, tangent, new Vector2( 0, 0 ), color );
		var b = new MeshVertex( origin.Position + width - height, normal, tangent, new Vector2( width.Length / texSize, 0 ), color );
		var c = new MeshVertex( origin.Position + width + height, normal, tangent, new Vector2( width.Length / texSize, height.Length / texSize ), color );
		var d = new MeshVertex( origin.Position - width + height, normal, tangent, new Vector2( 0, height.Length / texSize ), color );

		vertices.Add( a );
		vertices.Add( b );
		vertices.Add( c );
		vertices.Add( c );
		vertices.Add( d );
		vertices.Add( a );
	}

	[StructLayout( LayoutKind.Sequential )]
	struct MeshVertex
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Vector3 Tangent;
		public Vector2 TexCoord;
		public Color Color;

		public MeshVertex( Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord, Color color )
		{
			Position = position;
			Normal = normal;
			Tangent = tangent;
			TexCoord = texCoord;
			Color = color;
		}

		public static readonly VertexAttribute[] Layout =
		[
			new VertexAttribute( VertexAttributeType.Position, VertexAttributeFormat.Float32, 3 ),
			new VertexAttribute( VertexAttributeType.Normal, VertexAttributeFormat.Float32, 3 ),
			new VertexAttribute( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 3 ),
			new VertexAttribute( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2 ),
			new VertexAttribute( VertexAttributeType.Color, VertexAttributeFormat.Float32, 4 )
		];
	}
}
