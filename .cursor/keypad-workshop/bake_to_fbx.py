"""Bake SMD to FBX with Source-forward (+X) face for S&Box."""
from __future__ import annotations

import math
from pathlib import Path

import bpy

smd_path = Path(r"D:\Sandbox DarkRP\.cursor\keypad-workshop\converted\keypad_0_0.smd")
fbx_path = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets\models\keypad\keypad.fbx")

nodes: dict[int, tuple[str, int]] = {}
skeleton: dict[int, list[float]] = {}
triangles: list[tuple[str, list]] = []
section = None
current_mat = "keypad_sheet"
pending: list = []

with smd_path.open(encoding="utf-8", errors="ignore") as f:
	for raw in f:
		line = raw.strip()
		if not line:
			continue
		if line in ("nodes", "skeleton", "triangles"):
			section = line
			continue
		if line == "end":
			section = None
			continue
		if section == "nodes":
			parts = line.split('"')
			nodes[int(parts[0].strip())] = (parts[1], int(parts[2].strip()))
		elif section == "skeleton":
			if line.startswith("time"):
				continue
			vals = [float(x) for x in line.split()]
			skeleton[int(vals[0])] = vals[1:7]
		elif section == "triangles":
			parts = line.split()
			if len(parts) >= 9 and parts[0].lstrip("-").replace(".", "", 1).isdigit():
				bone = int(parts[0])
				x, y, z = map(float, parts[1:4])
				nx, ny, nz = map(float, parts[4:7])
				u, v = map(float, parts[7:9])
				pending.append((bone, x, y, z, nx, ny, nz, u, v))
				if len(pending) == 3:
					triangles.append((current_mat, list(pending)))
					pending = []
			else:
				current_mat = line


def rot_matrix(rx: float, ry: float, rz: float):
	cx, sx = math.cos(rx), math.sin(rx)
	cy, sy = math.cos(ry), math.sin(ry)
	cz, sz = math.cos(rz), math.sin(rz)
	return [
		[cy * cz, sx * sy * cz - cx * sz, cx * sy * cz + sx * sz],
		[cy * sz, sx * sy * sz + cx * cz, cx * sy * sz - sx * cz],
		[-sy, sx * cy, cx * cy],
	]


def mul(R, v):
	return (
		R[0][0] * v[0] + R[0][1] * v[1] + R[0][2] * v[2],
		R[1][0] * v[0] + R[1][1] * v[1] + R[1][2] * v[2],
		R[2][0] * v[0] + R[2][1] * v[1] + R[2][2] * v[2],
	)


world: dict[int, tuple] = {}
for idx in sorted(nodes):
	_name, parent = nodes[idx]
	px, py, pz, rx, ry, rz = skeleton[idx]
	R = rot_matrix(rx, ry, rz)
	local_pos = (px, py, pz)
	if parent < 0:
		world[idx] = (R, local_pos)
	else:
		pR, pPos = world[parent]
		wpos = (
			pPos[0] + pR[0][0] * local_pos[0] + pR[0][1] * local_pos[1] + pR[0][2] * local_pos[2],
			pPos[1] + pR[1][0] * local_pos[0] + pR[1][1] * local_pos[1] + pR[1][2] * local_pos[2],
			pPos[2] + pR[2][0] * local_pos[0] + pR[2][1] * local_pos[1] + pR[2][2] * local_pos[2],
		)
		wR = [[0.0] * 3 for _ in range(3)]
		for i in range(3):
			for j in range(3):
				wR[i][j] = pR[i][0] * R[0][j] + pR[i][1] * R[1][j] + pR[i][2] * R[2][j]
		world[idx] = (wR, wpos)


def to_source_forward(p):
	"""Bone-posed mesh has face along +Z; rotate so face is +X (Source forward)."""
	x, y, z = p
	return (z, y, -x)


def to_source_forward_n(n):
	x, y, z = n
	return (z, y, -x)


verts = []
normals = []
uvs = []
faces = []
mat_names: list[str] = []
mat_lookup: dict[str, int] = {}
face_mats: list[int] = []

for mat, tri in triangles:
	if mat not in mat_lookup:
		mat_lookup[mat] = len(mat_names)
		mat_names.append(mat)
	face = []
	for bone, x, y, z, nx, ny, nz, u, v in tri:
		R, pos = world[bone]
		wp = (
			pos[0] + R[0][0] * x + R[0][1] * y + R[0][2] * z,
			pos[1] + R[1][0] * x + R[1][1] * y + R[1][2] * z,
			pos[2] + R[2][0] * x + R[2][1] * y + R[2][2] * z,
		)
		wn = mul(R, (nx, ny, nz))
		wp = to_source_forward(wp)
		wn = to_source_forward_n(wn)
		face.append(len(verts))
		verts.append(wp)
		normals.append(wn)
		uvs.append((u, v))
	faces.append(tuple(face))
	face_mats.append(mat_lookup[mat])

bpy.ops.wm.read_factory_settings(use_empty=True)
mesh = bpy.data.meshes.new("keypad")
mesh.from_pydata(verts, [], faces)
mesh.update()
uv_layer = mesh.uv_layers.new(name="UVMap")
for poly in mesh.polygons:
	for li in poly.loop_indices:
		vi = mesh.loops[li].vertex_index
		uv_layer.data[li].uv = uvs[vi]
mesh.normals_split_custom_set_from_vertices(normals)

obj = bpy.data.objects.new("keypad", mesh)
bpy.context.collection.objects.link(obj)
for mat_name in mat_names:
	mat = bpy.data.materials.new(name=mat_name)
	obj.data.materials.append(mat)
for i, poly in enumerate(mesh.polygons):
	poly.material_index = face_mats[i]

bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj

fbx_path.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
	filepath=str(fbx_path),
	use_selection=True,
	apply_unit_scale=True,
	apply_scale_options="FBX_SCALE_UNITS",
	bake_space_transform=True,
	object_types={"MESH"},
	add_leaf_bones=False,
	axis_forward="-Z",
	axis_up="Y",
)

xs = [v[0] for v in verts]
ys = [v[1] for v in verts]
zs = [v[2] for v in verts]
print("bounds X", min(xs), max(xs))
print("bounds Y", min(ys), max(ys))
print("bounds Z", min(zs), max(zs))
print("FBX", fbx_path, fbx_path.stat().st_size)
