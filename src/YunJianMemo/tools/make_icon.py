from PIL import Image, ImageDraw
from pathlib import Path

S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# soft outer glow
for i in range(26, 0, -1):
    a = int(4 + (26 - i) * 0.9)
    d.rounded_rectangle([i * 5, i * 5, S - i * 5, S - i * 5], radius=230, fill=(78, 205, 196, a))

# main tile with vertical gradient
tile = Image.new("RGBA", (S, S), (0, 0, 0, 0))
td = ImageDraw.Draw(tile)
for y in range(S):
    p = y / (S - 1)
    r = int(33 + (18 - 33) * p)
    g = int(48 + (28 - 48) * p)
    b = int(61 + (41 - 61) * p)
    td.line([(0, y), (S, y)], fill=(r, g, b, 255))
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([70, 70, S - 70, S - 70], radius=210, fill=255)
img.alpha_composite(Image.composite(tile, Image.new("RGBA", (S, S), (0, 0, 0, 0)), mask))

d = ImageDraw.Draw(img)
# border
d.rounded_rectangle([70, 70, S - 70, S - 70], radius=210, outline=(255, 255, 255, 38), width=8)

# notebook lines
for i, y in enumerate((380, 500, 620)):
    w = 330 if i < 2 else 220
    d.rounded_rectangle([250, y, 250 + w, y + 30], radius=15, fill=(255, 255, 255, 60 if i else 95))

# check mark
d.line([(255, 735), (410, 875), (760, 510)], fill=(110, 231, 216, 255), width=86, joint="curve")
# focus dot
d.ellipse([700, 180, 850, 330], fill=(255, 122, 122, 255))
d.ellipse([730, 210, 820, 300], fill=(255, 170, 160, 255))

out = Path(__file__).resolve().parent.parent / "Assets" / "app.ico"
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
frames = []
for s in sizes:
    frames.append(img.resize((s, s), Image.LANCZOS))
frames[-1].save(out, format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
print(out)
