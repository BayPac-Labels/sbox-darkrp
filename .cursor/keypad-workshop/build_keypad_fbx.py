"""Build keypad as Blender mesh (Y-up, face +Y) → FBX for S&Box ModelDoc."""
from __future__ import annotations

import bpy
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets")
TEX = ROOT / "materials" / "models" / "keypad" / "keypad_face.png"
FBX = ROOT / "models" / "keypad" / "keypad.fbx"
VMDL = ROOT / "models" / "keypad" / "keypad.vmdl"

HOUSING = (36, 36, 36)
SCREEN = (50, 75, 50)
ABORT = (120, 25, 25)
OK = (25, 120, 25)
DIGIT = (120, 120, 120)
BLACK = (0, 0, 0)
FACE_W, FACE_H = 512, 940


def try_font(size: int):
	for name in ("C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf"):
		try:
			return ImageFont.truetype(name, size)
		except OSError:
			pass
	return ImageFont.load_default()


def draw_centered(draw, box, text, font, fill):
	x, y, w, h = box
	bbox = draw.textbbox((0, 0), text, font=font)
	tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
	draw.text((x + (w - tw) / 2 - bbox[0], y + (h - th) / 2 - bbox[1]), text, font=font, fill=fill)


def write_texture():
	img = Image.new("RGB", (FACE_W, FACE_H), HOUSING)
	draw = ImageDraw.Draw(img)

	def box(nx, ny, nw, nh):
		return int(nx * FACE_W), int(ny * FACE_H), int(nw * FACE_W), int(nh * FACE_H)

	sx, sy, sw, sh = box(0.075, 0.04, 0.85, 0.25)
	draw.rectangle([sx, sy, sx + sw, sy + sh], fill=SCREEN)

	ax, ay, aw, ah = box(0.075, 0.32, 0.455, 0.125)
	ox, oy, ow, oh = box(0.57, 0.32, 0.355, 0.125)
	draw.rectangle([ax, ay, ax + aw, ay + ah], fill=ABORT)
	draw.rectangle([ox, oy, ox + ow, oy + oh], fill=OK)
	draw_centered(draw, (ax, ay, aw, ah), "ABORT", try_font(42), BLACK)
	draw_centered(draw, (ox, oy, ow, oh), "OK", try_font(52), BLACK)

	font = try_font(64)
	for i in range(9):
		col, row = i % 3, i // 3
		dx, dy, dw, dh = box(0.075 + 0.3 * col, 0.475 + (0.5 / 3) * row, 0.25, 0.13)
		draw.rectangle([dx, dy, dx + dw, dy + dh], fill=DIGIT)
		draw_centered(draw, (dx, dy, dw, dh), str(i + 1), font, BLACK)

	TEX.parent.mkdir(parents=True, exist_ok=True)
	img.save(TEX)
	print("texture", TEX)


def build_mesh():
	bpy.ops.wm.read_factory_settings(use_empty=True)

	# Blender Y-up: face on +Y so ModelDoc/FBX maps it to Source +Z.
	# X = width, Z = height (becomes Source Y), Y = thickness.
	hw, hh, t = 3.0, 5.5, 1.0
	verts = [
		(-hw, 0.0, -hh),  # 0 back-left-bottom
		(hw, 0.0, -hh),  # 1
		(hw, 0.0, hh),  # 2
		(-hw, 0.0, hh),  # 3
		(-hw, t, -hh),  # 4 front-left-bottom
		(hw, t, -hh),  # 5
		(hw, t, hh),  # 6
		(-hw, t, hh),  # 7 front-left-top
	]
	# Faces (0-based). Front (+Y): 4,5,6,7 — view from +Y: right=+X, up=+Z
	# UV: u 0 at -X, 1 at +X; v 0 at +Z (top), 1 at -Z (bottom)
	faces = [
		(4, 5, 6, 7),  # +Y front
		(0, 3, 2, 1),  # -Y back
		(1, 2, 6, 5),  # +X
		(0, 4, 7, 3),  # -X
		(3, 7, 6, 2),  # +Z top
		(0, 1, 5, 4),  # -Z bottom
	]

	mesh = bpy.data.meshes.new("keypad")
	mesh.from_pydata(verts, [], faces)
	uv = mesh.uv_layers.new(name="UVMap")

	# Assign UVs per corner of each polygon
	# Front poly index 0 verts loop order 4,5,6,7
	front_uv = {
		4: (0.0, 1.0),  # -X -Z bottom-left
		5: (1.0, 1.0),  # +X -Z bottom-right
		6: (1.0, 0.0),  # +X +Z top-right
		7: (0.0, 0.0),  # -X +Z top-left
	}
	housing = (0.002, 0.002)

	for poly in mesh.polygons:
		for li in poly.loop_indices:
			vi = mesh.loops[li].vertex_index
			if poly.index == 0:
				uv.data[li].uv = front_uv[vi]
			else:
				uv.data[li].uv = housing

	# Triangulate for safety
	obj = bpy.data.objects.new("keypad", mesh)
	bpy.context.collection.objects.link(obj)
	bpy.context.view_layer.objects.active = obj
	obj.select_set(True)
	bpy.ops.object.mode_set(mode="EDIT")
	bpy.ops.mesh.select_all(action="SELECT")
	bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
	bpy.ops.object.mode_set(mode="OBJECT")

	# Recalc normals outward
	bpy.ops.object.shade_flat()
	bpy.ops.object.mode_set(mode="EDIT")
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
		path_mode="AUTO",
	)

	pts = [v.co.copy() for v in obj.data.vertices]
	print("blender bounds X", min(p.x for p in pts), max(p.x for p in pts))
	print("blender bounds Y", min(p.y for p in pts), max(p.y for p in pts))
	print("blender bounds Z", min(p.z for p in pts), max(p.z for p in pts))
	print("FBX", FBX, FBX.stat().st_size)


def write_vmdl():
	VMDL.write_text(
		"""<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:modeldoc30:version{8c2d7a91-9c42-4bf0-883a-5a3b1762d4f1} -->
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
""",
		encoding="utf-8",
	)
	print("vmdl", VMDL)


if __name__ == "__main__":
	write_texture()
	build_mesh()
	write_vmdl()
