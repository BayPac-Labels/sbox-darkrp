"""Bake SMD bind pose to OBJ for S&Box."""
from __future__ import annotations

import math
from pathlib import Path

smd_path = Path(r"D:\Sandbox DarkRP\.cursor\keypad-workshop\converted\keypad_0_0.smd")
obj_path = Path(r"D:\Sandbox DarkRP\.cursor\keypad-workshop\converted\keypad.obj")

nodes: dict[int, tuple[str, int]] = {}
skeleton: dict[int, list[float]] = {}
triangles: list[tuple[str, list]] = []
section = None
current_mat = "material"
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
					triangles.append((current_mat, pending))
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

print("bones:")
for i, (R, pos) in world.items():
	print(i, nodes[i][0], "pos", pos)

verts = []
uvs = []
normals = []
faces_by_mat: dict[str, list] = {}
for mat, tri in triangles:
	face = []
	for bone, x, y, z, nx, ny, nz, u, v in tri:
		R, pos = world[bone]
		wp = (
			pos[0] + R[0][0] * x + R[0][1] * y + R[0][2] * z,
			pos[1] + R[1][0] * x + R[1][1] * y + R[1][2] * z,
			pos[2] + R[2][0] * x + R[2][1] * y + R[2][2] * z,
		)
		wn = mul(R, (nx, ny, nz))
		verts.append(wp)
		normals.append(wn)
		uvs.append((u, v))
		face.append(len(verts))
	faces_by_mat.setdefault(mat, []).append(face)

with obj_path.open("w", encoding="utf-8") as f:
	f.write("# keypad baked from props_lab/keypad.mdl\n")
	f.write("mtllib keypad.mtl\n")
	for x, y, z in verts:
		f.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
	for u, v in uvs:
		f.write(f"vt {u:.6f} {v:.6f}\n")
	for x, y, z in normals:
		f.write(f"vn {x:.6f} {y:.6f} {z:.6f}\n")
	for mat, faces in faces_by_mat.items():
		f.write(f"usemtl {mat}\n")
		for face in faces:
			a, b, c = face
			f.write(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n")

with obj_path.with_suffix(".mtl").open("w", encoding="utf-8") as f:
	for mat in faces_by_mat:
		f.write(f"newmtl {mat}\nKd 1 1 1\nmap_Kd {mat}.png\n\n")

print("wrote", obj_path, "verts", len(verts), "tris", sum(len(v) for v in faces_by_mat.values()))
xs = [v[0] for v in verts]
ys = [v[1] for v in verts]
zs = [v[2] for v in verts]
print("bounds X", min(xs), max(xs))
print("bounds Y", min(ys), max(ys))
print("bounds Z", min(zs), max(zs))
