import bpy
from pathlib import Path

obj_path = Path(r"D:\Sandbox DarkRP\.cursor\keypad-workshop\converted\keypad.obj")
fbx_path = Path(r"D:\Sandbox DarkRP\.cursor\keypad-workshop\converted\keypad.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=str(obj_path), forward_axis="Y", up_axis="Z")
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
	filepath=str(fbx_path),
	use_selection=True,
	apply_scale_options="FBX_SCALE_UNITS",
	bake_space_transform=False,
	object_types={"MESH"},
	add_leaf_bones=False,
	axis_forward="-Z",
	axis_up="Y",
)
print("FBX written", fbx_path, "size", fbx_path.stat().st_size)
