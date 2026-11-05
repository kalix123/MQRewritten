#!/usr/bin/env python3
"""Draws the MQ Rewritten logo from shapes defined in this file: no fonts, no source images, no image generator.

    python tools/make_logo.py

Needs Pillow. Run it from anywhere; it overwrites the logo files listed at the bottom.
"""
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
SS = 3  # drawn this much larger, then shrunk for smooth edges

INK = (26, 16, 8, 255)
SIDE = (150, 66, 12, 255)
TOP = (255, 226, 92)
BOTTOM = (243, 134, 22)
CREAM = (255, 248, 232, 255)
BANANA_TOP = (255, 236, 120)
BANANA_BOTTOM = (250, 190, 40)
STEM = (96, 60, 22, 255)
RIBBON = (24, 24, 26, 255)
ORANGE = (242, 146, 29, 255)

# Block letters on a box one unit tall: outline, then any holes.
M = [(0, 1), (0.07, 0), (0.36, 0), (0.6, 0.4), (0.84, 0), (1.13, 0), (1.2, 1), (0.86, 1), (0.83, 0.52), (0.64, 0.84),
     (0.56, 0.84), (0.37, 0.52), (0.34, 1)]
BLOCKS = {
    "R": (0.8, [(0, 0), (0.6, 0), (0.78, 0.13), (0.78, 0.43), (0.63, 0.55), (0.82, 1), (0.53, 1), (0.38, 0.6), (0.27, 0.6),
                (0.27, 1), (0, 1)], [(0.27, 0.21), (0.5, 0.21), (0.5, 0.39), (0.27, 0.39)]),
    "E": (0.66, [(0, 0), (0.66, 0), (0.66, 0.22), (0.27, 0.22), (0.27, 0.39), (0.58, 0.39), (0.58, 0.6), (0.27, 0.6),
                 (0.27, 0.78), (0.66, 0.78), (0.66, 1), (0, 1)], None),
    "W": (1.1, [(0, 0), (0.26, 0), (0.33, 0.56), (0.45, 0.08), (0.65, 0.08), (0.77, 0.56), (0.84, 0), (1.1, 0), (0.92, 1),
                (0.66, 1), (0.55, 0.56), (0.44, 1), (0.18, 1)], None),
    "I": (0.27, [(0, 0), (0.27, 0), (0.27, 1), (0, 1)], None),
    "T": (0.72, [(0, 0), (0.72, 0), (0.72, 0.22), (0.49, 0.22), (0.49, 1), (0.23, 1), (0.23, 0.22), (0, 0.22)], None),
    "N": (0.84, [(0, 0), (0.27, 0), (0.57, 0.54), (0.57, 0), (0.84, 0), (0.84, 1), (0.57, 1), (0.27, 0.46), (0.27, 1), (0, 1)], None),
}


def blank(w, h):
    return Image.new("L", (w, h), 0)


def turned(points, cx, cy, degrees):
    a = math.radians(degrees)
    return [(cx + (x - cx) * math.cos(a) - (y - cy) * math.sin(a), cy + (x - cx) * math.sin(a) + (y - cy) * math.cos(a))
            for x, y in points]


def ellipse(cx, cy, rx, ry, degrees=0, steps=120):
    points = [(cx + rx * math.cos(i / steps * math.tau), cy + ry * math.sin(i / steps * math.tau)) for i in range(steps)]
    return turned(points, cx, cy, degrees)


def shifted(mask, dx, dy):
    moved = blank(*mask.size)
    moved.paste(mask, (int(dx), int(dy)))
    return moved


def grown(mask, radius):
    """The mask with a rounded edge pushed out by about radius."""
    return mask.filter(ImageFilter.GaussianBlur(radius)).point(lambda v: 255 if v > 12 else 0)


def gradient(size, top, bottom, y0, y1):
    w, h = size
    column = Image.new("RGBA", (1, h))
    for y in range(h):
        t = min(1, max(0, (y - y0) / max(1, y1 - y0)))
        column.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,))
    return column.resize((w, h))


def chunky(canvas, mask, depth, outline, top=TOP, bottom=BOTTOM):
    """Stamps a mask as a game-title letter: dark outline, a solid side going down-right, a gradient face, a lit edge."""
    box = mask.getbbox()
    side = blank(*mask.size)
    for i in range(1, int(depth) + 1):
        side = ImageChops.lighter(side, shifted(mask, i * 0.55, i))
    body = ImageChops.lighter(side, mask)
    canvas.paste(INK, (0, 0), grown(body, outline))
    canvas.paste(SIDE, (0, 0), side)
    canvas.paste(gradient(mask.size, top, bottom, box[1], box[3]), (0, 0), mask)
    edge = ImageChops.subtract(mask, shifted(mask, 0, max(2, depth * 0.3)))
    canvas.paste((255, 255, 255, 120), (0, 0), edge)


def letter_m(size, x, y, height, degrees):
    mask = blank(*size)
    points = [(x + px * height, y + py * height) for px, py in M]
    ImageDraw.Draw(mask).polygon(turned(points, x + height * 0.6, y + height / 2, degrees), fill=255)
    return mask


def letter_q(size, cx, cy, height, degrees):
    mask = blank(*size)
    draw = ImageDraw.Draw(mask)
    draw.polygon(ellipse(cx, cy, height * 0.6, height * 0.54, degrees), fill=255)
    draw.polygon(ellipse(cx, cy, height * 0.235, height * 0.25, degrees + 20), fill=0)
    return mask


def banana(canvas, cx, cy, length, degrees, outline):
    """A banana curving through (cx, cy): two arcs that meet at the tips, with a stalk."""
    half = length / 2
    ts = [i / 40 * 2 - 1 for i in range(41)]
    belly = [(cx + t * half, cy + half * 0.62 * (1 - t * t)) for t in ts]
    back = [(cx + t * half, cy + half * 0.14 * (1 - t * t)) for t in reversed(ts)]
    shape = turned(belly + back, cx, cy, degrees)
    stalk = turned([(cx + half * 0.92, cy - half * 0.03), (cx + half * 1.2, cy - half * 0.2), (cx + half * 1.28, cy - half * 0.08),
                    (cx + half * 1.02, cy + half * 0.1)], cx, cy, degrees)
    mask = blank(*canvas.size)
    draw = ImageDraw.Draw(mask)
    draw.polygon(shape, fill=255)
    draw.polygon(stalk, fill=255)
    canvas.paste(INK, (0, 0), grown(mask, outline))
    box = mask.getbbox()
    canvas.paste(gradient(canvas.size, BANANA_TOP, BANANA_BOTTOM, box[1], box[3]), (0, 0), mask)
    tip = blank(*canvas.size)
    ImageDraw.Draw(tip).polygon(stalk, fill=255)
    canvas.paste(STEM, (0, 0), tip)
    ridge = turned([(cx + t * half * 0.8, cy + half * 0.36 * (1 - t * t)) for t in ts], cx, cy, degrees)
    ImageDraw.Draw(canvas).line(ridge, fill=(214, 140, 24, 255), width=max(1, int(length * 0.018)))


def mark(canvas, x, y, height, letters="MQ"):
    """MQ at (x, y): each letter tipped a little the other way, with a banana for the Q's tail."""
    depth, outline = height * 0.11, height * 0.026
    size = canvas.size
    if letters == "MQ":
        chunky(canvas, letter_m(size, x, y, height, -5), depth, outline)
        qx, qy = x + height * 1.86, y + height * 0.56
    else:
        qx, qy = x + height * 0.6, y + height * 0.54
    chunky(canvas, letter_q(size, qx, qy, height, 4), depth, outline)
    banana(canvas, qx + height * 0.3, qy + height * 0.3, height * 0.72, 40, outline * 0.9)


def logo(size, letters="MQ"):
    s = size * SS
    canvas = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    if letters == "MQ":
        height = s * 0.335
        mark(canvas, s * 0.075, s * 0.29, height)
    else:
        height = s * 0.62
        mark(canvas, s * 0.08, s * 0.06, height, "Q")
    return canvas.resize((size, size), Image.LANCZOS)


def ribbon(canvas, cx, cy, width, height, text):
    """A dark slanted plate with an orange edge and the text in block letters."""
    lean = height * 0.28
    plate = [(cx - width / 2 + lean, cy - height / 2), (cx + width / 2 + lean, cy - height / 2),
             (cx + width / 2 - lean, cy + height / 2), (cx - width / 2 - lean, cy + height / 2)]
    mask = blank(*canvas.size)
    ImageDraw.Draw(mask).polygon(plate, fill=255)
    canvas.paste(INK, (0, 0), grown(mask, height * 0.1))
    canvas.paste(ORANGE, (0, 0), grown(mask, height * 0.05))
    canvas.paste(RIBBON, (0, 0), mask)

    letter_height = height * 0.62
    gap = letter_height * 0.13
    total = sum(BLOCKS[c][0] * letter_height for c in text) + gap * (len(text) - 1)
    x = cx - total / 2
    y = cy - letter_height / 2
    draw = ImageDraw.Draw(canvas)
    shear = lean / (height / 2) * 0.5
    for char in text:
        width_of, outer, hole = BLOCKS[char]

        def place(points):
            return [(x + px * letter_height + (0.5 - py) * letter_height * shear, y + py * letter_height) for px, py in points]

        draw.polygon(place(outer), fill=CREAM)
        if hole:
            draw.polygon(place(hole), fill=RIBBON)
        x += width_of * letter_height + gap


def banner(width, height):
    """The full lockup: MQ with REWRITTEN on a plate across the bottom."""
    w, h = width * SS, height * SS
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    letter = h * 0.53
    ribbon(canvas, w / 2, h * 0.84, letter * 3.1, h * 0.2, "REWRITTEN")
    mark(canvas, w / 2 - letter * 1.32, h * 0.05, letter)
    return canvas.resize((width, height), Image.LANCZOS)


def on_canvas(image, width, height):
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    canvas.alpha_composite(image, ((width - image.width) // 2, (height - image.height) // 2))
    return canvas


def icon(path):
    # The two smallest sizes drop the M: it can't be read at that size.
    sizes = {16: "Q", 32: "Q", 48: "MQ", 64: "MQ", 128: "MQ", 256: "MQ"}
    images = [logo(size, letters) for size, letters in sizes.items()]
    images[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=images[:-1])


def main():
    # Cropped to the drawing, so it fills the website's fixed logo height.
    site = logo(768)
    left, top, right, bottom = site.getbbox()
    site.crop((left - 6, top - 6, right + 6, bottom + 6)).save(ROOT / "Web.Razor/wwwroot/logo.png")
    on_canvas(logo(300), 500, 300).save(ROOT / "LogoIcon.png")
    banner(1200, 640).save(ROOT / "LogoBanner.png")
    icon(ROOT / "Init/Icon.ico")
    icon(ROOT / "Init/wwwroot/favicon.ico")


if __name__ == "__main__":
    main()
