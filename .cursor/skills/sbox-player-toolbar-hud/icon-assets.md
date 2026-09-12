# HUD weapon icon assets

## Target format

- PNG, RGBA, **512×512**
- Opaque white RGB `(255,255,255)` for the silhouette
- Transparent background (alpha 0)
- Soft AA on edges only (alpha 30–160); body alpha 255
- **Horizontal footprint** matching physgun: bbox aspect ~**1.5–2.5** for most tools/guns (rifles may be longer); silhouette spans most of canvas width
- Fill ratio roughly **12–30%** of the canvas (chunky tools/guns ~15–25%; very long rifles ~12–15% after thickening)
- Filename: `*_wide.png` for remakes pointed at by prefabs; `*_hud.png` OK for canonical refs (physgun/toolgun/keys/hands)

Canonical reference: `DarkRP2/Assets/ui/weapons/physgun_hud.png`

Also healthy: `toolgun_hud.png`, `keys_hud.png`, `hands_hud.png`, `usp_wide.png`, `mp5_wide.png`

Healthy stats: nonzero-alpha pixels with **avg alpha ≈ 246–255**; fill ≳ **12%**.

Broken thin line-art often shows **avg alpha ≈ 60**. Tiny legacy killicons on 1024 canvases often show **fill ≲ 8%** and look skinny vs physgun in the hotbar.

## Generate (Cursor GenerateImage)

Prompt pattern:

> Game HUD inventory icon. Flat solid white (#FFFFFF) filled silhouette of \<SUBJECT\>, side profile facing left, horizontal. Chunky opaque killicon style like Garry's Mod / matching physgun visual weight. Pure black background. No outlines, no internal detail lines, no shading, no text, no color. Square 1:1. Silhouette longer and wider — fills most of the horizontal width like physgun_hud, substantial thickness, modest padding only.

Pass `reference_image_paths: ["DarkRP2/Assets/ui/weapons/physgun_hud.png"]` and `aspect_ratio: "1:1"`.

For firearms, also pass the old killicon as a second reference so the weapon stays recognizable.

## Convert black-background silhouette → HUD PNG

```python
from PIL import Image, ImageFilter

src = "physgun_source.png"   # white on black
dst = "physgun_wide.png"

im = Image.open(src).convert("RGBA")
out = []
for r, g, b, a in im.getdata():
	lum = 0.299 * r + 0.587 * g + 0.114 * b
	if a < 8 or lum < 40:
		out.append((255, 255, 255, 0))
	elif lum > 180:
		out.append((255, 255, 255, 255))
	else:
		out.append((255, 255, 255, int((lum - 40) * (255 / 140))))

result = Image.new("RGBA", im.size)
result.putdata(out)
bbox = result.getbbox()
if bbox:
	result = result.crop(bbox)
w, h = result.size
side = int(max(w, h) * 1.08)  # tight padding like physgun
canvas = Image.new("RGBA", (side, side), (255, 255, 255, 0))
canvas.paste(result, ((side - w) // 2, (side - h) // 2), result)
canvas = canvas.resize((512, 512), Image.Resampling.LANCZOS)
# harden soft alphas after LANCZOS
px = []
for r, g, b, a in canvas.getdata():
	if a < 20:
		px.append((255, 255, 255, 0))
	elif a > 180:
		px.append((255, 255, 255, 255))
	else:
		px.append((255, 255, 255, a))
canvas.putdata(px)
canvas.save(dst, "PNG")
```

Do **not** treat light-gray backgrounds as white — flood-fill / luminance thresholds will turn the whole frame into a white square.

### Thicken skinny silhouettes

If fill is still ≲ 10% after conversion (crowbar, sniper, shotgun, etc.), dilate alpha then re-center:

```python
from PIL import ImageFilter

im = Image.open(dst).convert("RGBA")
alpha = im.split()[3]
for _ in range(4, 7):  # more passes = chunkier
	alpha = alpha.filter(ImageFilter.MaxFilter(5))
# rebuild white+alpha, crop bbox, pad with 1.08, resize 512, harden alphas
```

For boxy icons that are too square (e.g. medkit), optionally stretch the cropped silhouette horizontally to aspect ~**1.85** before padding.

## Prefab snip

```json
"DisplayIcon": {
  "$compiler": "texture",
  "$source": "imagefile",
  "data": {
    "FilePath": "ui/weapons/usp_wide.png",
    "MaxSize": 4096,
    "ConvertHeightToNormals": false,
    "NormalScale": 1,
    "Rotate": 0,
    "FlipVertical": false,
    "FlipHorizontal": false,
    "Cropping": { "Left": 0, "Top": 0, "Right": 0, "Bottom": 0 },
    "Padding": { "Left": 0, "Top": 0, "Right": 0, "Bottom": 0 },
    "InvertColor": false,
    "Tint": "1,1,1,1",
    "Blur": 0,
    "Sharpen": 0,
    "Brightness": 1,
    "Contrast": 1,
    "Saturation": 1,
    "Hue": 0,
    "Colorize": false,
    "TargetColor": "1,1,1,1",
    "CacheToDisk": true
  },
  "compiled": null
},
"DisplayName": "USP"
```

### Editing pitfalls

- Prefer `$source: imagefile` over `svgsource` for hotbar icons (tint + cache are more reliable).
- If `DisplayIcon` is `null`, replace `null` only — never brace-scan into the following property (`DroppedGameObject`).
- Remove weapon `InventoryIconOverride` when switching a tinted slot to a white silhouette PNG.
- Remake **guns** when matching physgun / longer-wider requests — do not keep legacy `*_01.png` killicons in prefabs.

## Cache bust

If the in-game icon stays wrong after replacing pixels on the same path, save as a **new filename** (`*_wide.png`) and update `FilePath`.

## Quick stats check

```python
from PIL import Image
im = Image.open("ui/weapons/foo_wide.png").convert("RGBA")
nz = [a for *_, a in im.getdata() if a > 0]
xs, ys = [], []
w, h = im.size
for y in range(h):
	for x in range(w):
		if im.getpixel((x, y))[3] > 0:
			xs.append(x); ys.append(y)
bw, bh = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
print(f"fill={len(nz)/(w*h):.1%} avgA={sum(nz)/len(nz):.0f} aspect={bw/bh:.2f} bbox={bw}x{bh}")
```

Target: `avgA ≈ 246–255`, `fill ≳ 12%`, `aspect ≳ 1.5` (except intentionally chunky items like hands).
