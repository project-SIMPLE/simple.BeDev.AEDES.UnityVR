#!/usr/bin/env python3
"""
The one colour palette every Module 3 prop is painted from.

The props keep "one material per asset" (Asset Brief Sec.2) and still get more than one colour
each: every material samples the same 8x8 swatch texture, T_M3_PropPalette.png, and each part of
a mesh has all of its UVs sitting on the centre of one swatch. That is the same trick the rest of
Team Assets uses with its gradient atlases (a UV island parked on a patch of T_gradient_light_1),
just with flat swatches, so a fan can have a cream body, blue blades and grey wire at one draw
call, and the colour of a part is a name in this file rather than a hand-picked pixel.

Shared by make_final_models.py (which writes the UVs) and make_final_materials.py (which writes
the PNG and the materials that point at it), so the two cannot drift apart.

Rules:
  * Append new swatches at the END. The index is baked into every committed FBX's UVs, so
    reordering silently recolours every prop.
  * Colours are sRGB, the values the Unity material inspector shows; the texture is imported as
    sRGB and point-filtered with no mipmaps, so a swatch centre samples exactly this value.
"""

import struct
import zlib

GRID = 8            # 8 x 8 swatches
SWATCH_PX = 16      # each 16 x 16 px -> a 128 x 128 texture
SIZE = GRID * SWATCH_PX
TEXTURE = "T_M3_PropPalette"

SWATCHES = [
    # name            sRGB                 used by
    ("cream",        (0.88, 0.86, 0.80)),  # fan body and badge: cheap cream plastic
    ("fan_blade",    (0.58, 0.76, 0.84)),  # the translucent-blue blade plastic of a table fan
    ("fan_guard",    (0.72, 0.74, 0.76)),  # chromed guard wire
    ("dark",         (0.13, 0.13, 0.14)),  # motor cap, hub, buttons, torch body
    ("wood",         (0.45, 0.36, 0.26)),  # window-screen frame
    ("wood_dark",    (0.36, 0.28, 0.20)),  # screen beading
    ("net",          (0.92, 0.90, 0.84)),  # gathered netting
    ("cord",         (0.62, 0.46, 0.28)),  # the net's tie cord
    ("amber",        (0.82, 0.52, 0.16)),  # repellent bottle body
    ("white",        (0.93, 0.93, 0.91)),  # bottle trigger head, nozzle, cap
    ("label",        (0.36, 0.58, 0.40)),  # plain unprinted label band, no brand
    ("enamel",       (0.92, 0.92, 0.88)),  # enamelware cup
    ("enamel_blue",  (0.16, 0.30, 0.54)),  # its rim and handle
    ("water",        (0.56, 0.76, 0.86)),  # water surface
    ("cloth_a",      (0.52, 0.68, 0.66)),  # checked cloth, light square
    ("cloth_b",      (0.34, 0.50, 0.50)),  # checked cloth, dark square
    ("silver",       (0.76, 0.77, 0.79)),  # torch bezel
    ("lens",         (0.96, 0.93, 0.72)),  # torch lens
    ("red",          (0.76, 0.18, 0.15)),  # torch button, health-centre cross
    ("whitewash",    (0.92, 0.90, 0.84)),  # health-centre walls, window frames
    ("roof",         (0.62, 0.26, 0.20)),  # health-centre roof sheeting
    ("concrete",     (0.64, 0.63, 0.60)),  # plinth, porch slab
    ("door",         (0.20, 0.42, 0.56)),  # painted door
    ("glass",        (0.30, 0.42, 0.52)),  # window glass
]

_INDEX = {name: i for i, (name, _) in enumerate(SWATCHES)}
assert len(SWATCHES) <= GRID * GRID


def index(name):
    return _INDEX[name]


def rgb(name):
    return SWATCHES[_INDEX[name]][1]


def uv(name):
    """Centre of the swatch, in UV space (v = 0 at the bottom of the image, as Blender and
    Unity both read it)."""
    i = _INDEX[name]
    col, row = i % GRID, i // GRID
    return ((col + 0.5) / GRID, (row + 0.5) / GRID)


def png_rows():
    """The texture as RGBA byte rows, top row first (PNG order). Swatch row 0 is at the
    BOTTOM of the image so that uv() above lands on it; unused swatches are magenta, so a
    part whose UVs were never set is obvious in the game."""
    rows = []
    for py in range(SIZE):
        row_from_bottom = (SIZE - 1 - py) // SWATCH_PX
        row = bytearray()
        for px in range(SIZE):
            col = px // SWATCH_PX
            i = row_from_bottom * GRID + col
            r, g, b = SWATCHES[i][1] if i < len(SWATCHES) else (1.0, 0.0, 1.0)
            row += bytes((round(r * 255), round(g * 255), round(b * 255), 255))
        rows.append(bytes(row))
    return rows


def write_png(path, width, height, rows):
    """Minimal RGBA8 PNG writer (stdlib only, so the materials script needs no PIL)."""
    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + r for r in rows)
    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw, 9))
           + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


if __name__ == "__main__":
    for name, c in SWATCHES:
        print(f"{index(name):2d} {name:12s} rgb={c} uv={tuple(round(x, 4) for x in uv(name))}")
