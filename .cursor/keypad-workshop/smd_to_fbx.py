"""Import a Source SMD mesh and export FBX for S&Box ModelDoc."""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Euler, Vector


def parse_smd(path: Path):
	nodes: dict[int, tuple[str, int]] = {}
	skeleton: dict[int, tuple[float, float, float, float, float, float]] = {}
	triangles: list[tuple[str, list[tuple]]] = []

	section = None
	current_mat = None
	pending_verts: list[tuple] = []

	with path.open("r", encoding="utf-8", errors="ignore") as f:
		for raw in f:
			line = raw.strip()
			if not line:
				continue
			if line in ("nodes", "skeleton", "triangles", "end"):
				if line == "end":
					section = None
				else:
					section = line
				continue

			if section == "nodes":
				parts = line.split('"')
				idx = int(parts[0].strip())
				name = parts[1]
				parent = int(parts[2].strip())
				nodes[idx] = (name, parent)
			elif section == "skeleton":
				if line.startswith("time"):
					continue
				vals = [float(x) for x in line.split()]
				skeleton[int(vals[0])] = tuple(vals[1:7])
			elif section == "triangles":
				parts = line.split()
				if len(parts) == 1 or (len(parts) >= 1 and not parts[0].lstrip("-").replace(".", "", 1).isdigit()):
					# material name line
					if pending_verts:
						# incomplete triangle discarded
						pending_verts = []
					current_mat = line
					continue

				# bone x y z nx ny nz u v [links...]
				bone = int(parts[0])
				x, y, z = float(parts[1]), float(parts[2]), float(parts[3])
				nx, ny, nz = float(parts[4]), float(parts[5]), float(parts[6])
				u, v = float(parts[7]), float(parts[8])
				pending_verts.append((bone, x, y, z, nx, ny, nz, u, v))
				if len(pending_verts) == 3:
					triangles.append((current_mat or "material", list(pending_verts)))
					pending_verts = []

	return nodes, skeleton, triangles


def clear_scene():
	bpy.ops.object.select_all(action="SELECT")
	bpy.ops.object.delete(use_global=False)
	for block in bpy.data.meshes:
		bpy.data.meshes.remove(block)
	for block in bpy.data.materials:
		bpy.data.materials.remove(block)
	for block in bpy.data.armatures:
		bpy.data.armatures.remove(block)


def build(smd_path: Path, fbx_path: Path):
	clear_scene()
	nodes, skeleton, triangles = parse_smd(smd_path)

	# Armature
	arm_data = bpy.data.armatures.new("keypad_armature")
	arm_obj = bpy.data.objects.new("keypad_armature", arm_data)
	bpy.context.collection.objects.link(arm_obj)
	bpy.context.view_layer.objects.active = arm_obj
	bpy.ops.object.mode_set(mode="EDIT")

	edit_bones = {}
	for idx in sorted(nodes.keys()):
		name, parent = nodes[idx]
		bone = arm_data.edit_bones.new(name)
		pos = skeleton.get(idx, (0, 0, 0, 0, 0, 0))
		loc = Vector((pos[0], pos[1], pos[2]))
		# SMD stores parent-relative transforms; apply simply for this tiny prop
		bone.head = loc
		bone.tail = loc + Vector((0, 0, 1.0 if loc.length < 0.01 else max(loc.length * 0.25, 0.5)))
		edit_bones[idx] = bone
		if parent >= 0 and parent in edit_bones:
			bone.parent = edit_bones[parent]
			# Parent-relative: convert to armature space roughly by adding parent head
			bone.head = bone.parent.head + loc
			bone.tail = bone.head + Vector((0, 0, 0.5))

	bpy.ops.object.mode_set(mode="OBJECT")

	# Mesh
	verts = []
	normals = []
	uvs = []
	faces = []
	mat_names = []
	mat_index_for_face = []
	mat_lookup: dict[str, int] = {}

	for mat_name, tri in triangles:
		if mat_name not in mat_lookup:
			mat_lookup[mat_name] = len(mat_names)
			mat_names.append(mat_name)
		face = []
		for v in tri:
			face.append(len(verts))
			verts.append((v[1], v[2], v[3]))
			normals.append((v[4], v[5], v[6]))
			uvs.append((v[7], v[8]))
		faces.append(tuple(face))
		mat_index_for_face.append(mat_lookup[mat_name])

	mesh = bpy.data.meshes.new("keypad")
	mesh.from_pydata(verts, [], faces)
	mesh.update()

	# UVs
	uv_layer = mesh.uv_layers.new(name="UVMap")
	for poly in mesh.polygons:
		for li in poly.loop_indices:
			vi = mesh.loops[li].vertex_index
			uv_layer.data[li].uv = (uvs[vi][0], uvs[vi][1])

	# Custom normals
	mesh.normals_split_custom_set_from_vertices(normals)

	obj = bpy.data.objects.new("keypad", mesh)
	bpy.context.collection.objects.link(obj)

	for mat_name in mat_names:
		mat = bpy.data.materials.new(name=mat_name)
		mat.use_nodes = True
		obj.data.materials.append(mat)

	for i, poly in enumerate(mesh.polygons):
		poly.material_index = mat_index_for_face[i]

	# Skin to armature (all weight on bone 0 / base)
	mod = obj.modifiers.new("Armature", "ARMATURE")
	mod.object = arm_obj
	vg = obj.vertex_groups.new(name=nodes[0][0])
	vg.add(list(range(len(verts))), 1.0, "REPLACE")

	obj.parent = arm_obj

	# Source uses Z-up; Blender too by default for from_pydata. FBX export with -Z forward, Y up is Blender default.
	# S&Box expects Source-ish units (inches). Leave scale 1.
	fbx_path.parent.mkdir(parents=True, exist_ok=True)
	bpy.ops.object.select_all(action="DESELECT")
	obj.select_set(True)
	arm_obj.select_set(True)
	bpy.context.view_layer.objects.active = arm_obj

	bpy.ops.export_scene.fbx(
		filepath=str(fbx_path),
		use_selection=True,
		apply_scale_options="FBX_SCALE_UNITS",
		bake_space_transform=False,
		object_types={"ARMATURE", "MESH"},
		use_mesh_modifiers=True,
		add_leaf_bones=False,
		path_mode="AUTO",
		axis_forward="-Z",
		axis_up="Y",
	)
	print(f"Exported {fbx_path} verts={len(verts)} faces={len(faces)} mats={mat_names}")


if __name__ == "__main__":
	argv = sys.argv
	argv = argv[argv.index("--") + 1 :] if "--" in argv else []
	smd = Path(argv[0])
	fbx = Path(argv[1])
	build(smd, fbx)
