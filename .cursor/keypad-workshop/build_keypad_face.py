"""Build Willox keypad with face on +Z (Wire pitch-90 wall placement)."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(r"D:\Sandbox DarkRP\DarkRP2\Assets")
FACE_W, FACE_H = 512, 940
OUT_TEX = ROOT / "materials" / "models" / "keypad" / "keypad_face.png"
OUT_OBJ = ROOT / "models" / "keypad" / "keypad.obj"
OUT_MTL = ROOT / "models" / "keypad" / "keypad.mtl"

HOUSING = (36, 36, 36)
SCREEN = (50, 75, 50)
ABORT = (120, 25, 25)
OK = (25, 120, 25)
DIGIT = (120, 120, 120)
BLACK = (0, 0, 0)


def try_font(size: int):
	for name in (
		"C:/Windows/Fonts/arialbd.ttf",
		"C:/Windows/Fonts/arial.ttf",
	):
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


def main():
	img = Image.new("RGB", (FACE_W, FACE_H), HOUSING)
	draw = ImageDraw.Draw(img)

	def box(nx, ny, nw, nh):
		return int(nx * FACE_W), int(ny * FACE_H), int(nw * FACE_W), int(nh * FACE_H)

	sx, sy, sw, sh = box(0.075, 0.04, 0.85, 0.25)
	draw.rectangle([sx, sy, sx + sw, sy + sh], fill=SCREEN)

	ax, ay, aw, ah = box(0.075, 0.32, 0.85 / 2 - 0.02 + 0.05, 0.125)
	ox, oy, ow, oh = box(0.57, 0.32, 0.85 / 2 - 0.02 - 0.05, 0.125)
	draw.rectangle([ax, ay, ax + aw, ay + ah], fill=ABORT)
	draw.rectangle([ox, oy, ox + ow, oy + oh], fill=OK)
	draw_centered(draw, (ax, ay, aw, ah), "ABORT", try_font(42), BLACK)
	draw_centered(draw, (ox, oy, ow, oh), "OK", try_font(52), BLACK)

	digit_font = try_font(64)
	for i in range(9):
		col, row = i % 3, i // 3
		dx, dy, dw, dh = box(0.075 + 0.3 * col, 0.475 + (0.5 / 3) * row, 0.25, 0.13)
		draw.rectangle([dx, dy, dx + dw, dy + dh], fill=DIGIT)
		draw_centered(draw, (dx, dy, dw, dh), str(i + 1), digit_font, BLACK)

	OUT_TEX.parent.mkdir(parents=True, exist_ok=True)
	img.save(OUT_TEX)

	# Face on +Z for Wire-style placement (LookAt(normal) * pitch 90).
	# X = width (±3), Y = height (±5.5), Z = thickness (0..1), face at Z=1.
	# Looking at +Z: +X right, +Y up → Willox u left-to-right needs -X or flip.
	# Willox u=0 left. On wall after pitch, world-up ≈ local Y.
	# Texture u: 0 at -X (left when +Y is up viewing +Z? View +Z: right=+X, up=+Y.
	# So left = -X → u=0 at -X, u=1 at +X. v=0 at +Y (top), v=1 at -Y.
	verts = [
		(-3.0, -5.5, 0.0),  # 1
		(3.0, -5.5, 0.0),  # 2
		(3.0, 5.5, 0.0),  # 3
		(-3.0, 5.5, 0.0),  # 4
		(-3.0, -5.5, 1.0),  # 5
		(3.0, -5.5, 1.0),  # 6
		(3.0, 5.5, 1.0),  # 7
		(-3.0, 5.5, 1.0),  # 8
	]
	# Front +Z verts: 5(-X,-Y), 6(+X,-Y), 7(+X,+Y), 8(-X,+Y)
	# UV: 8=(-X,+Y)=(0,0), 7=(+X,+Y)=(1,0), 6=(+X,-Y)=(1,1), 5=(-X,-Y)=(0,1)

	lines = [
		"# Willox keypad — face +Z (Wire pitch-90)",
		"mtllib keypad.mtl",
		"o keypad",
	]
	for x, y, z in verts:
		lines.append(f"v {x:.6f} {y:.6f} {z:.6f}")
	lines += [
		"vt 0.001000 0.001000",  # 1 housing
		"vt 0.000000 0.000000",  # 2 -X +Y
		"vt 1.000000 0.000000",  # 3 +X +Y
		"vt 1.000000 1.000000",  # 4 +X -Y
		"vt 0.000000 1.000000",  # 5 -X -Y
		"vn 0 0 1",
		"vn 0 0 -1",
		"vn 1 0 0",
		"vn -1 0 0",
		"vn 0 1 0",
		"vn 0 -1 0",
		"usemtl keypad_face",
		# +Z face CCW when viewed from +Z
		"f 8/2/1 5/5/1 6/4/1",
		"f 8/2/1 6/4/1 7/3/1",
		# -Z back
		"f 1/1/2 2/1/2 3/1/2",
		"f 1/1/2 3/1/2 4/1/2",
		# +X
		"f 2/1/3 6/1/3 7/1/3",
		"f 2/1/3 7/1/3 3/1/3",
		# -X
		"f 1/1/4 4/1/4 8/1/4",
		"f 1/1/4 8/1/4 5/1/4",
		# +Y
		"f 4/1/5 3/1/5 7/1/5",
		"f 4/1/5 7/1/5 8/1/5",
		# -Y
		"f 1/1/6 5/1/6 6/1/6",
		"f 1/1/6 6/1/6 2/1/6",
	]
	OUT_OBJ.write_text("\n".join(lines) + "\n", encoding="utf-8")
	OUT_MTL.write_text("newmtl keypad_face\nKd 1 1 1\n", encoding="utf-8")
	print("wrote", OUT_TEX, OUT_OBJ)


if __name__ == "__main__":
	main()
