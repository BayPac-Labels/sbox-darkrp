import bpy
from pathlib import Path

obj_path = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets\models\keypad\keypad.obj")
fbx_path = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets\models\keypad\keypad.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)
# Keep Source axes: X forward/thickness, Y right/width, Z up/height
bpy.ops.wm.obj_import(
	filepath=str(obj_path),
	forward_axis="NEGATIVE_Z",
	up_axis="Y",
)

obj = next(o for o in bpy.context.scene.objects if o.type == "MESH")
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj

# After default OBJ import ( -Z forward, Y up), rotate into Source X-forward Z-up:
# Blender Y-up -Z-forward → Source Z-up X-forward
# Rotate +90 around X then... simpler: rebuild mesh in place from known verts.

bpy.ops.object.delete()

# Build mesh directly with Source coordinates
verts = [
	(0.0, -3.0, -5.5),
	(1.0, -3.0, -5.5),
	(1.0, 3.0, -5.5),
	(0.0, 3.0, -5.5),
	(0.0, -3.0, 5.5),
	(1.0, -3.0, 5.5),
	(1.0, 3.0, 5.5),
	(0.0, 3.0, 5.5),
]
# Faces (0-based): front CCW from +X, then sides
faces = [
	(6, 2, 1), (6, 1, 5),  # +X front
	(0, 3, 7), (0, 7, 4),  # -X
	(2, 6, 7), (2, 7, 3),  # +Y
	(0, 4, 5), (0, 5, 1),  # -Y
	(4, 7, 6), (4, 6, 5),  # +Z
	(0, 1, 2), (0, 2, 3),  # -Z
]
uvs_per_loop = []
# Front UVs for loops of first two tris: 7,3,2 and 7,2,6 → indices 6,2,1 and 6,1,5
front_uv = {
	6: (0.0, 0.0),  # +Y+Z
	2: (0.0, 1.0),  # +Y-Z
	1: (1.0, 1.0),  # -Y-Z
	5: (1.0, 0.0),  # -Y+Z
}
housing = (0.001, 0.001)

mesh = bpy.data.meshes.new("keypad")
mesh.from_pydata(verts, [], faces)
uv_layer = mesh.uv_layers.new(name="UVMap")
for poly in mesh.polygons:
	for li in poly.loop_indices:
		vi = mesh.loops[li].vertex_index
		if poly.index < 2:
			uv_layer.data[li].uv = front_uv[vi]
		else:
			uv_layer.data[li].uv = housing

mesh.calc_normals()
obj = bpy.data.objects.new("keypad", mesh)
bpy.context.collection.objects.link(obj)
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj

bpy.ops.export_scene.fbx(
	filepath=str(fbx_path),
	use_selection=True,
	apply_unit_scale=True,
	apply_scale_options="FBX_SCALE_ALL",
	bake_space_transform=False,
	object_types={"MESH"},
	add_leaf_bones=False,
	# Identity-ish: keep our vertex positions as authored
	axis_forward="-Z",
	axis_up="Y",
)

pts = [v.co for v in mesh.vertices]
print("local bounds X", min(p.x for p in pts), max(p.x for p in pts))
print("local bounds Y", min(p.y for p in pts), max(p.y for p in pts))
print("local bounds Z", min(p.z for p in pts), max(p.z for p in pts))
print("FBX", fbx_path, fbx_path.stat().st_size)
