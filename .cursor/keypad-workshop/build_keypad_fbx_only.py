import bpy
from pathlib import Path

FBX = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets\models\keypad\keypad.fbx")
VMDL = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets\models\keypad\keypad.vmdl")

bpy.ops.wm.read_factory_settings(use_empty=True)

hw, hh, t = 3.0, 5.5, 1.0
verts = [
    (-hw, 0.0, -hh),
    ( hw, 0.0, -hh),
    ( hw, 0.0,  hh),
    (-hw, 0.0,  hh),
    (-hw, t,   -hh),
    ( hw, t,   -hh),
    ( hw, t,    hh),
    (-hw, t,    hh),
]
faces = [
    (4, 5, 6, 7),
    (0, 3, 2, 1),
    (1, 2, 6, 5),
    (0, 4, 7, 3),
    (3, 7, 6, 2),
    (0, 1, 5, 4),
]
mesh = bpy.data.meshes.new("keypad")
mesh.from_pydata(verts, [], faces)
uv = mesh.uv_layers.new(name="UVMap")
front_uv = {
    4: (0.0, 1.0),
    5: (1.0, 1.0),
    6: (1.0, 0.0),
    7: (0.0, 0.0),
}
housing = (0.002, 0.002)
for poly in mesh.polygons:
    for li in poly.loop_indices:
        vi = mesh.loops[li].vertex_index
        uv.data[li].uv = front_uv[vi] if poly.index == 0 else housing

obj = bpy.data.objects.new("keypad", mesh)
bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode="OBJECT")

FBX.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=str(FBX),
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    bake_space_transform=True,
    object_types={"MESH"},
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    axis_forward="-Z",
    axis_up="Y",
)
pts = [v.co for v in obj.data.vertices]
print("bounds X", min(p.x for p in pts), max(p.x for p in pts))
print("bounds Y", min(p.y for p in pts), max(p.y for p in pts))
print("bounds Z", min(p.z for p in pts), max(p.z for p in pts))
print("FBX", FBX, FBX.stat().st_size)

VMDL.write_text("""<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:modeldoc30:version{8c2d7a91-9c42-4bf0-883a-5a3b1762d4f1} -->
{
	rootNode =
	{
		_class = "RootNode"
		children =
		[
			{
				_class = "MaterialGroupList"
				children =
				[
					{
						_class = "DefaultMaterialGroup"
						remaps = [  ]
						use_global_default = true
						global_default_material = "materials/models/keypad/keypad_body.vmat"
					},
				]
			},
			{
				_class = "RenderMeshList"
				children =
				[
					{
						_class = "RenderMeshFile"
						filename = "models/keypad/keypad.fbx"
						import_translation = [ 0.0, 0.0, 0.0 ]
						import_rotation = [ 0.0, 0.0, 0.0 ]
						import_scale = 1.0
						align_origin_x_type = "None"
						align_origin_y_type = "None"
						align_origin_z_type = "None"
						parent_bone = ""
						import_filter =
						{
							exclude_by_default = false
							exception_list = [  ]
						}
					},
				]
			},
			{
				_class = "PhysicsShapeList"
				children =
				[
					{
						_class = "PhysicsShapeBox"
						surface_prop = "metalpanel"
						collision_tags = "solid"
						origin = [ 0.0, 0.0, 0.5 ]
						angles = [ 0.0, 0.0, 0.0 ]
						dimensions = [ 6.0, 11.0, 1.0 ]
					},
				]
			},
		]
		model_archetype = ""
		primary_associated_entity = ""
		anim_graph_name = ""
		base_model_name = ""
	}
}
""", encoding="utf-8")
print("vmdl written")
