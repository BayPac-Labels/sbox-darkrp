"""Bake keypad to Source model space using $definebone rotz=90 (matches $bbox)."""
from __future__ import annotations

import math
from pathlib import Path

smd_path = Path(r"D:\Sandbox DarkRP\.cursor\keypad-workshop\decompiled\keypad_reference.smd")
obj_path = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets\models\keypad\keypad.obj")

# $definebone "base" "" 0 0 0 0 0 90
angle = math.radians(90.0)
cz, sz = math.cos(angle), math.sin(angle)


def xform(p):
	x, y, z = p
	# Rz(90): (x,y,z) -> (-y, x, z)
	return (-y, x, z)


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
		if section != "triangles":
			continue
		parts = line.split()
		if len(parts) >= 9 and parts[0].lstrip("-").replace(".", "", 1).isdigit():
			x, y, z = map(float, parts[1:4])
			nx, ny, nz = map(float, parts[4:7])
			u, v = map(float, parts[7:9])
			pending.append((x, y, z, nx, ny, nz, u, v))
			if len(pending) == 3:
				triangles.append((current_mat, list(pending)))
				pending = []
		else:
			current_mat = line

verts = []
uvs = []
normals = []
faces_by_mat: dict[str, list] = {}

for mat, tri in triangles:
	face = []
	for x, y, z, nx, ny, nz, u, v in tri:
		verts.append(xform((x, y, z)))
		normals.append(xform((nx, ny, nz)))
		uvs.append((u, v))
		face.append(len(verts))
	faces_by_mat.setdefault(mat, []).append(face)

with obj_path.open("w", encoding="utf-8") as f:
	f.write("# HL2 props_lab/keypad.mdl — Willox keypad\n")
	f.write("# Bind pose from $definebone rotz=90; $bbox ~ X thickness, Y width, Z height\n")
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
		f.write(f"newmtl {mat}\nKd 1 1 1\n\n")

xs = [v[0] for v in verts]
ys = [v[1] for v in verts]
zs = [v[2] for v in verts]
print("QC $bbox: -0.281 -3.281 -5.791  1.29 3.281 5.771")
print("mesh X", min(xs), max(xs))
print("mesh Y", min(ys), max(ys))
print("mesh Z", min(zs), max(zs))
print("tris", sum(len(v) for v in faces_by_mat.values()))
# Sample a button-face normal (should be near +X)
print("sample normals", normals[20], normals[50], normals[100])
