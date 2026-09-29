#!/usr/bin/env blender --python
"""
Final art for the Module 3 (AEDES) props -- the detail pass.

This keeps the contract from Documentation/Module3-Placeholder-Assets.md: the same node names
(Fan_Base / Fan_Head / Fan_Blade, Screen_Mesh), the same Socket_* empties at the same points, the
same origins, and export over the same Team Assets/Models/<name>/<name>.fbx path with the
existing .fbx.meta untouched, so swapping the art is a one-file change with no scene or
prefab-path churn. What changed in this pass:

  * Far more geometry. Lathes run at 40-56 segments instead of 14-18, edges get a two-segment
    bevel, and shading is smooth-by-angle (flat faces stay flat, rounded ones read round). The
    Quest 3 budgets in Module3PlaceholderPrefabs.cs were raised to match; each prop stays under
    8000 triangles and most are well under, which is nothing next to the scene's houses.
  * More than one colour per prop, still with ONE material per prop. Every part's UVs sit on a
    swatch of the shared palette texture (Tools/Module3/m3_palette.py, T_M3_PropPalette.png), so
    the fan has a cream body, blue blades and grey guard wire in a single draw call. This is the
    same idea as the gradient atlases the rest of Team Assets use.
  * The window screens' netting is a separate child mesh, Screen_Mesh, so the prefab builder can
    give it an alpha-cutout wire-grid material and the screen is something you see through rather
    than a board over the window. The torn screen has a real ragged hole with two curled flaps.
  * Things that were wrong are fixed in passing: the cup's handle was a ring stuck flat against
    the side, the fan's blades were boxes with guard bars running through them.

Run:
    blender -b --factory-startup --python Tools/Module3/make_final_models.py [-- --out DIR]

Format is FBX, not glTF: Documentation/Module3-Asset-Brief.md Sec.2 is explicit that glTF/.glb
is accepted for placeholders only, and this project's whole import pipeline (materialImportMode,
stable GUIDs, the prefab builder) is built around FBX. The axis/unit conversion below is the one
from make_placeholders.py -- see Module3-Placeholder-Assets.md "Axis and unit traps" for why it is
done this way and not left to the exporter.
"""

import bpy, bmesh, math, os, sys, json
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import m3_palette as PAL

# ---------------------------------------------------------------------------
# Reference dimensions MEASURED from existing repo assets (unchanged from
# make_placeholders.py -- these fit the shipped house and bed, not invented).
# ---------------------------------------------------------------------------
WINDOW_W        = 1.50   # SM_WindowLeft/Right: two 0.75 m leaves, full opening
WINDOW_H        = 1.00
WINDOW_REVEAL   = 0.12   # frame reveal depth the screen must sit inside
NET_TOP         = 1.39   # SM_MosquitoNet canopy height -- rolled net shares it

# Triangle budgets, per asset. Raised in the detail pass (they were 500-3000); the
# PlaceholderAssetTests ceiling of 8000 per prop still holds for every one of them.
TRI_BUDGET = {
    "SM_ElectricFan":          8000,
    "SM_WindowScreen":         3000,
    "SM_WindowScreenTorn":     4000,
    "SM_MosquitoNet_RolledUp": 5000,
    "SM_RepellentBottle":      3000,
    "SM_DrinkingVessel":       3000,
    "SM_Cloth":                4000,
    "SM_Torch":                3000,
    "SM_HealthCentre":         4000,
}

SCREEN_CELL = 0.015      # wire pitch of the insect mesh, metres -- one tile of T_M3_ScreenMesh

# ---------------------------------------------------------------------------
# Primitive + mesh-building helpers. Everything is authored in Blender's Z-up
# metres; convert_to_yup() rotates it into Unity's axes at the end.
# ---------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0

def _added():
    return bpy.context.active_object

def select_only(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o

def box(size, at=(0, 0, 0), rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at, rotation=rot)
    o = _added()
    o.scale = size
    return o

def cyl(r, h, at=(0, 0, 0), seg=32, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=seg,
                                        location=at, rotation=rot)
    return _added()

def sphere(r, at=(0, 0, 0), seg=24, rings=None, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=seg,
                                         ring_count=rings or max(6, seg // 2), location=at)
    o = _added()
    if scale != (1, 1, 1):
        o.scale = scale
    return o

def torus(major, minor, at=(0, 0, 0), rot=(0, 0, 0), mseg=40, nseg=8):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=mseg, minor_segments=nseg,
                                     location=at, rotation=rot)
    return _added()

def seg_between(p0, p1, r, seg=8):
    """A thin cylinder spanning two points -- guard wires, cord ends."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler()
    return cyl(r, d.length, at=(p0 + p1) / 2, seg=seg, rot=rot)

def new_mesh_obj(bm, name, at=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = at
    select_only(o)
    return o

def revolve(profile, seg=40, at=(0, 0, 0), name="Revolve", ripple=None):
    """Surface of revolution around local Z from an ordered (radius, z) profile.

    A point at radius 0 closes as a pole. Walk the profile with the outside on your right (up
    the outside of a cup, over the rim, down the inside) and every face comes out facing the
    right way without a normal recalculation. `ripple(row, theta, z)` returns a per-vertex
    radius multiplier, which is how the rolled net gets its gathered folds.
    """
    bm = bmesh.new()
    rows = []
    for i, (r, z) in enumerate(profile):
        if r <= 1e-7:
            rows.append(bm.verts.new((0.0, 0.0, z)))
            continue
        ring = []
        for j in range(seg):
            th = 2 * math.pi * j / seg
            rr = r * (ripple(i, th, z) if ripple else 1.0)
            ring.append(bm.verts.new((rr * math.cos(th), rr * math.sin(th), z)))
        rows.append(ring)
    for a, b in zip(rows, rows[1:]):
        pa, pb = isinstance(a, bmesh.types.BMVert), isinstance(b, bmesh.types.BMVert)
        if pa and pb:
            continue
        for j in range(seg):
            k = (j + 1) % seg
            if pa:
                bm.faces.new((a, b[k], b[j]))
            elif pb:
                bm.faces.new((a[j], a[k], b))
            else:
                bm.faces.new((a[j], a[k], b[k], b[j]))
    return new_mesh_obj(bm, name, at)

def grid_surface(fn, nu, nv, name="Grid", at=(0, 0, 0), uv_fn=None):
    """An open parametric sheet: fn(i, j) -> point, for a (nu x nv) lattice of vertices.
    uv_fn(point) -> (u, v) if the sheet needs real UVs; otherwise paint() puts every loop on
    a palette swatch afterwards."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    verts = [[bm.verts.new(fn(i, j)) for j in range(nv)] for i in range(nu)]
    for i in range(nu - 1):
        for j in range(nv - 1):
            f = bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
            if uv_fn:
                for l in f.loops:
                    l[uv].uv = uv_fn(l.vert.co)
    return new_mesh_obj(bm, name, at)

def wedge(pts_bottom, thickness, at=(0, 0, 0), rot=(0, 0, 0), name="Wedge"):
    """An extruded flat polygon (a list of (x, y) points, CCW)."""
    bm = bmesh.new()
    bverts = [bm.verts.new((x, y, 0.0)) for x, y in pts_bottom]
    bm.faces.new(bverts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    r = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, vec=(0, 0, thickness),
                         verts=[v for v in r['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_mesh_obj(bm, name, at)
    o.rotation_euler = rot
    return o

def apply_rotation(o):
    select_only(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

def solidify(o, thickness, offset=0.0):
    select_only(o)
    mod = o.modifiers.new("Solidify", 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = offset
    mod.use_even_offset = True
    bpy.ops.object.modifier_apply(modifier=mod.name)

def bevel(o, width, segments=2, angle_deg=35):
    select_only(o)
    mod = o.modifiers.new("Bevel", 'BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(angle_deg)
    bpy.ops.object.modifier_apply(modifier=mod.name)

def delete_verts(o, pred):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    doomed = [v for v in bm.verts if pred(v.co)]
    bmesh.ops.delete(bm, geom=doomed, context='VERTS')
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()

def _uv_layer(me):
    return me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")

def paint(o, swatch):
    """Park every UV of the mesh on the centre of one palette swatch: the whole part takes that
    colour from the shared palette texture. Returns o so it can be used inline."""
    me = o.data
    uvl = _uv_layer(me)
    u, v = PAL.uv(swatch)
    uvl.data.foreach_set("uv", [u, v] * len(me.loops))
    return o

def paint_faces(o, swatch, pred):
    """Recolour the faces for which pred(centre, normal) holds -- a rim band, a rear cap, the
    checks on a cloth. Coordinates are the mesh's own (before any transform is applied)."""
    me = o.data
    uvl = _uv_layer(me)
    u, v = PAL.uv(swatch)
    for p in me.polygons:
        if pred(p.center, p.normal):
            for li in range(p.loop_start, p.loop_start + p.loop_total):
                uvl.data[li].uv = (u, v)
    return o

def join(parts, name, bevel_width=0.0015, bevel_seg=2, smooth_deg=40, bevel_angle=35):
    """Join parts into one mesh, apply every transform, round the sharp edges with a small
    bevel (a toon-friendly highlight instead of razor primitive edges) and shade smooth by
    angle, so flat faces stay flat and the bevels and lathes read as rounded."""
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel_width > 0:
        bevel(o, bevel_width, bevel_seg, bevel_angle)
    select_only(o)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth_deg))
    o.name = name
    o.data.name = name
    return o

def socket(name, at=(0, 0, 0), facing=None, parent=None):
    """An empty marking an attachment point. See make_placeholders.py's docstring for why
    `facing` is expressed in this authoring frame and re-derived by convert_to_yup()."""
    bpy.ops.object.empty_add(type='PLAIN_AXES', radius=0.05, location=at)
    e = _added()
    e.name = name
    if facing is not None:
        f = Vector(facing).normalized()
        e.rotation_euler = f.to_track_quat('Z', 'Y').to_euler()
        e["m3_facing"] = list(f)
    if parent:
        e.parent = parent
        e.matrix_parent_inverse = Matrix.Identity(4)
    return e

def child_of(o, parent):
    o.parent = parent
    o.matrix_parent_inverse = Matrix.Identity(4)
    return o


YUP = Matrix.Rotation(math.radians(-90), 4, 'X')   # Blender +Z up -> Unity +Y up


def convert_to_yup():
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.data.transform(YUP)
        o.location = YUP @ Vector(o.location)
        if o.type == 'EMPTY':
            f = o.get("m3_facing")
            o.rotation_euler = ((YUP @ Vector(f)).to_track_quat('Z', 'Y').to_euler()
                                if f else (0.0, 0.0, 0.0))
    bpy.context.view_layer.update()

def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

def polar(c):
    return math.hypot(c.x, c.y)

# ---------------------------------------------------------------------------
# Assets. The fan, screens and net face Blender -Y, which convert_to_yup() turns into Unity +Z.
# ---------------------------------------------------------------------------

def electric_fan():
    """Table fan (the view scales it 2.5x into a floor fan, so it is looked at closely).
    Envelope held to the verified 0.306 x 0.453 x 0.21: guard ring radius 0.145 + 0.008 wire,
    hub at 0.300, foot radius 0.105. Inside that: a lathed foot-and-column pedestal with three
    push buttons, a ball neck and a yoke with pivot pins, a domed motor housing with a dark
    rear vent, a real wire guard (rings, a badge plate and 36 radial wires), and three broad
    twisted, cambered blades on a domed hub.
    """
    HUB_Z = 0.300
    GR, GT = 0.145, 0.008          # guard ring: major / wire radius
    FOOT_R = 0.105
    FRONT_Y, BACK_Y = -0.078, 0.006

    # --- Fan_Base ---
    parts = []
    ped = revolve([
        (0.000, 0.000), (0.098, 0.000), (FOOT_R, 0.005), (FOOT_R, 0.015), (0.099, 0.023),
        (0.082, 0.029), (0.056, 0.034), (0.036, 0.041), (0.028, 0.052), (0.026, 0.066),
        (0.026, 0.184), (0.031, 0.196), (0.037, 0.206), (0.037, 0.222), (0.030, 0.230),
        (0.000, 0.232)], seg=40, name="_ped")
    parts.append(paint(ped, "cream"))
    for i, dx in enumerate((-0.026, 0.0, 0.026)):                       # speed buttons
        parts.append(paint(cyl(0.0075, 0.012, at=(dx, -0.062, 0.031), seg=16),
                           "red" if i == 0 else "dark"))
    parts.append(paint(sphere(0.030, at=(0, 0.010, 0.236), seg=20), "cream"))   # ball neck
    for sx in (-1, 1):                                                   # yoke cheeks + pins
        parts.append(paint(box((0.008, 0.034, 0.064), at=(sx * 0.062, 0.045, 0.268)), "cream"))
        parts.append(paint(cyl(0.011, 0.012, at=(sx * 0.068, 0.045, HUB_Z), seg=16,
                               rot=(0, math.radians(90), 0)), "dark"))
    parts.append(paint(box((0.132, 0.034, 0.010), at=(0, 0.045, 0.239)), "cream"))
    # One-segment bevel, corners only: the pedestal is already a smooth lathe.
    base = join(parts, "Fan_Base", bevel_seg=1, bevel_angle=50)

    # --- Fan_Head ---
    parts = []
    housing = revolve([
        (0.000, -0.058), (0.030, -0.0575), (0.046, -0.052), (0.055, -0.040), (0.058, -0.020),
        (0.058, 0.006), (0.054, 0.020), (0.044, 0.030), (0.028, 0.034), (0.000, 0.035)],
        seg=40, at=(0, 0.045, HUB_Z), name="_housing")
    paint(housing, "cream")
    paint_faces(housing, "dark", lambda c, n: c.z < -0.050)             # rear vent cap
    housing.rotation_euler = (math.radians(90), 0, 0)                   # local +Z -> world -Y
    apply_rotation(housing)
    parts.append(housing)
    for y in (FRONT_Y, BACK_Y):                                          # the two big rings
        parts.append(paint(torus(GR, GT, at=(0, y, HUB_Z), rot=(math.radians(90), 0, 0),
                                 mseg=44, nseg=8), "fan_guard"))
    parts.append(paint(torus(0.092, 0.0035, at=(0, FRONT_Y - 0.004, HUB_Z),
                             rot=(math.radians(90), 0, 0), mseg=40, nseg=6), "fan_guard"))
    parts.append(paint(cyl(0.036, 0.004, at=(0, FRONT_Y - 0.007, HUB_Z), seg=32,
                           rot=(math.radians(90), 0, 0)), "cream"))      # badge plate, no logo
    for i in range(24):                                                  # front wires
        th = 2 * math.pi * i / 24 + math.pi / 24
        c, s = math.cos(th), math.sin(th)
        parts.append(paint(seg_between((0.034 * c, FRONT_Y - 0.007, HUB_Z + 0.034 * s),
                                       (GR * c, FRONT_Y, HUB_Z + GR * s), 0.0016, seg=6),
                           "fan_guard"))
    parts.append(paint(cyl(0.030, 0.004, at=(0, BACK_Y + 0.001, HUB_Z), seg=24,
                           rot=(math.radians(90), 0, 0)), "fan_guard"))
    for i in range(12):                                                  # back wires
        th = 2 * math.pi * i / 12
        c, s = math.cos(th), math.sin(th)
        parts.append(paint(seg_between((0.028 * c, BACK_Y, HUB_Z + 0.028 * s),
                                       (GR * c, BACK_Y, HUB_Z + GR * s), 0.0016, seg=6),
                           "fan_guard"))
    for i in range(8):                                                   # ring-to-ring bridges
        th = 2 * math.pi * i / 8 + math.pi / 8
        parts.append(paint(box((0.005, BACK_Y - FRONT_Y, 0.005),
                               at=(GR * math.cos(th), (FRONT_Y + BACK_Y) / 2, HUB_Z + GR * math.sin(th))),
                           "fan_guard"))
    # No bevel here: it would put rounded corners on the caps of 36 wires and triple the count.
    head = join(parts, "Fan_Head", bevel_width=0.0)

    # --- Fan_Blade: authored around the origin so its own origin is the spin axis ---
    hub = revolve([(0.000, -0.014), (0.018, -0.014), (0.025, -0.010), (0.026, 0.006),
                   (0.022, 0.014), (0.012, 0.019), (0.000, 0.021)], seg=32, name="_hub")
    paint(hub, "dark")
    hub.rotation_euler = (math.radians(90), 0, 0)                       # dome to the front (-Y)
    apply_rotation(hub)
    parts = [hub]
    NU, NV = 12, 7
    for k in range(3):
        phi = math.radians(k * 120)
        R = Vector((math.cos(phi), 0, math.sin(phi)))       # radial, in the disc plane (X-Z)
        Cf = Vector((-math.sin(phi), 0, math.cos(phi)))     # chord direction, flat
        N = Vector((0, -1, 0))                              # the spin axis, pointing forward

        def leaf_pt(i, j):
            t = i / (NU - 1)
            v = -1 + 2 * j / (NV - 1)
            u = 0.020 + (0.132 - 0.020) * t
            w = 0.046 * (0.45 + 0.55 * t) * math.sqrt(max(0.0, 1 - t ** 8))   # rounded tip
            alpha = math.radians(34 - 16 * t)                                   # more pitch at the root
            C = Cf * math.cos(alpha) + N * math.sin(alpha)
            n = -Cf * math.sin(alpha) + N * math.cos(alpha)
            return R * u + C * (v * w) + n * (0.10 * w * (1 - v * v))          # slight dish

        leaf = grid_surface(leaf_pt, NU, NV, name=f"_leaf{k}")
        solidify(leaf, 0.003)
        parts.append(paint(leaf, "fan_blade"))
    blade = join(parts, "Fan_Blade", bevel_width=0.0)

    child_of(blade, head)
    blade.location = (0, -0.036, HUB_Z)
    child_of(head, base)
    socket("Socket_Mount", at=(0, 0, 0), parent=base)
    return base, [base, head, blade]


def _screen_mesh(bays, z0, z1, torn):
    """The netting: real quads with tiling UVs (one T_M3_ScreenMesh tile per SCREEN_CELL), in
    its own object so the prefab builder can give it the cutout material. The torn bay is a
    lattice with a ragged hole cut out of it and two curled flaps hanging from the hole's edge.
    Everything is single-sided; the material renders both faces."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    def tile_uv(co):
        return (co.x / SCREEN_CELL, co.z / SCREEN_CELL)

    def quad(x0, x1, zz0, zz1):
        vs = [bm.verts.new((x0, 0, zz0)), bm.verts.new((x1, 0, zz0)),
              bm.verts.new((x1, 0, zz1)), bm.verts.new((x0, 0, zz1))]
        f = bm.faces.new(vs)
        for l in f.loops:
            l[uv].uv = tile_uv(l.vert.co)

    intact = bays if not torn else bays[:1]
    for x0, x1 in intact:
        quad(x0, x1, z0, z1)

    if torn:
        x0, x1 = bays[1]
        cx, cz = (x0 + x1) / 2 + 0.02, z0 + (z1 - z0) * 0.58
        RX, RZ = 0.23, 0.19

        def rho(th):                                    # the ragged outline, unit radius
            return 1 + 0.18 * math.sin(5 * th) + 0.12 * math.cos(3 * th + 1)

        def inside(x, z):
            dx, dz = (x - cx) / RX, (z - cz) / RZ
            return math.hypot(dx, dz) < rho(math.atan2(dz, dx))

        cell = 0.025
        nx, nz = int(round((x1 - x0) / cell)), int(round((z1 - z0) / cell))
        for i in range(nx):
            for j in range(nz):
                ax, bx = x0 + (x1 - x0) * i / nx, x0 + (x1 - x0) * (i + 1) / nx
                az, bz = z0 + (z1 - z0) * j / nz, z0 + (z1 - z0) * (j + 1) / nz
                if not inside((ax + bx) / 2, (az + bz) / 2):
                    quad(ax, bx, az, bz)

        # Two flaps: strips hanging from the hole's edge, curling out of the plane (towards
        # -Y, the room side) and sagging. Their reach sets the torn screen's depth.
        for th0, th1, rc, kappa in ((math.radians(95), math.radians(170), 0.085, 1.35),
                                     (math.radians(245), math.radians(320), 0.060, 1.60)):
            NS, NT = 9, 6
            grid = []
            for si in range(NS):
                th = th0 + (th1 - th0) * si / (NS - 1)
                ex, ez = cx + RX * rho(th) * math.cos(th), cz + RZ * rho(th) * math.sin(th)
                d = Vector((cx - ex, 0, cz - ez)).normalized()
                row = []
                for ti in range(NT):
                    t = ti / (NT - 1)
                    p = (Vector((ex, 0, ez)) + d * (rc * math.sin(kappa * t))
                         + Vector((0, -1, 0)) * (rc * (1 - math.cos(kappa * t)))
                         + Vector((0, 0, -1)) * (0.02 * t * t))
                    row.append(bm.verts.new(p))
                grid.append(row)
            for si in range(NS - 1):
                for ti in range(NT - 1):
                    f = bm.faces.new((grid[si][ti], grid[si + 1][ti], grid[si + 1][ti + 1], grid[si][ti + 1]))
                    for l in f.loops:
                        l[uv].uv = tile_uv(l.vert.co)

    o = new_mesh_obj(bm, "Screen_Mesh")
    select_only(o)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    return o


def window_screen(torn=False):
    """Insect screen for the Lao house window: a bevelled timber frame with a centre mullion and
    dark beading strips holding the netting in each bay; the netting itself is Screen_Mesh."""
    fw, d = 0.034, 0.040
    W, H = WINDOW_W, WINDOW_H
    assert d <= WINDOW_REVEAL, "screen deeper than the measured window reveal"
    mw = fw * 0.8
    z0, z1 = fw, H - fw
    bays = [(-(W / 2 - fw), -mw / 2), (mw / 2, W / 2 - fw)]

    parts = []
    for size, at in (((W, d, fw), (0, 0, fw / 2)), ((W, d, fw), (0, 0, H - fw / 2)),
                     ((fw, d, H - 2 * fw), (-(W - fw) / 2, 0, H / 2)),
                     ((fw, d, H - 2 * fw), ((W - fw) / 2, 0, H / 2)),
                     ((mw, d * 0.85, H - 2 * fw), (0, 0, H / 2))):
        parts.append(paint(box(size, at=at), "wood"))
    for x0, x1 in bays:                                                  # beading, room side
        bw, by, bd = 0.010, -0.008, 0.009
        parts.append(paint(box((x1 - x0, bd, bw), at=((x0 + x1) / 2, by, z0 + bw / 2)), "wood_dark"))
        parts.append(paint(box((x1 - x0, bd, bw), at=((x0 + x1) / 2, by, z1 - bw / 2)), "wood_dark"))
        parts.append(paint(box((bw, bd, z1 - z0 - 2 * bw), at=(x0 + bw / 2, by, H / 2)), "wood_dark"))
        parts.append(paint(box((bw, bd, z1 - z0 - 2 * bw), at=(x1 - bw / 2, by, H / 2)), "wood_dark"))
    name = "SM_WindowScreenTorn" if torn else "SM_WindowScreen"
    frame = join(parts, name, bevel_width=0.003)

    mesh = child_of(_screen_mesh(bays, z0, z1, torn), frame)
    socket("Socket_Mount", at=(0, 0, 0), parent=frame)
    return frame, [frame, mesh]


def mosquito_net_rolled_up():
    """The "up" state of PF_MosquitoNet: gathered netting tied off in two places, with the
    knots and loose cord ends, a rolled hem at the bottom and a gather at the top. Shares
    the deployed net's origin and canopy height so the two prefabs swap at one transform.
    The folds are a ripple on the lathe radius that twists as it climbs."""
    prof = [(0.000, 0.800), (0.070, 0.804), (0.118, 0.818), (0.146, 0.846), (0.152, 0.885),
            (0.140, 0.930), (0.128, 0.958), (0.146, 0.995), (0.152, 1.040), (0.150, 1.075),
            (0.132, 1.108), (0.124, 1.128), (0.144, 1.165), (0.150, 1.210), (0.140, 1.255),
            (0.112, 1.300), (0.072, 1.345), (0.036, 1.375), (0.012, 1.387), (0.000, NET_TOP)]
    ties = (0.958, 1.128)

    def ripple(i, th, z):
        near_tie = min(abs(z - t) for t in ties)
        a = 0.015 + 0.030 * min(1.0, near_tie / 0.06)          # calmer under the cords
        a *= min(1.0, (z - 0.80) / 0.03, (NET_TOP - z) / 0.06)  # fade to the poles
        phase = 3.0 * (z - 0.8)
        return 1 + a * math.sin(11 * th + phase) + 0.012 * math.sin(23 * th - 2 * phase)

    bundle = paint(revolve(prof, seg=56, name="_bundle", ripple=ripple), "net")
    parts = [bundle]
    parts.append(paint(torus(0.100, 0.010, at=(0, 0, 0.808), mseg=40, nseg=8), "net"))   # rolled hem
    for z, r in zip(ties, (0.128, 0.124)):
        parts.append(paint(torus(r + 0.003, 0.006, at=(0, 0, z), mseg=48, nseg=8), "cord"))
        kx, ky = (r + 0.006) * math.cos(0.35), (r + 0.006) * math.sin(0.35)
        parts.append(paint(sphere(0.012, at=(kx, ky, z), seg=12), "cord"))              # knot
        for dz, dx in ((-0.075, 0.010), (-0.095, -0.012)):                              # loose ends
            parts.append(paint(seg_between((kx, ky, z - 0.006), (kx + dx, ky + 0.012, z + dz),
                                           0.0035, seg=6), "cord"))
    o = join(parts, "SM_MosquitoNet_RolledUp", bevel_width=0.0)
    socket("Socket_Mount", at=(0, 0, 0), parent=o)
    socket("Socket_Hook", at=(0, 0, NET_TOP), parent=o)
    return o, [o]


def repellent_bottle():
    """Small trigger-spray bottle: lathed amber body with a plain unprinted label band, a white
    collar, a rounded white trigger head, nozzle and lever. Generic, no brand marks."""
    body = revolve([(0.000, 0.000), (0.024, 0.001), (0.029, 0.004), (0.030, 0.010),
                    (0.030, 0.098), (0.028, 0.110), (0.021, 0.120), (0.017, 0.126),
                    (0.016, 0.132), (0.016, 0.146), (0.013, 0.150), (0.013, 0.162),
                    (0.000, 0.162)], seg=40, name="_body")
    paint(body, "amber")
    paint_faces(body, "label", lambda c, n: 0.028 < c.z < 0.086 and polar(c) > 0.02)
    paint_faces(body, "white", lambda c, n: c.z > 0.140)
    parts = [body]
    head = box((0.050, 0.032, 0.030), at=(-0.010, 0, 0.176))
    apply_rotation(head)                                    # bake the scale so the bevel is even
    bevel(head, 0.006, 3)
    parts.append(paint(head, "white"))
    parts.append(paint(cyl(0.006, 0.022, at=(-0.036, 0, 0.180), seg=16,
                           rot=(0, math.radians(90), 0)), "white"))          # nozzle
    parts.append(paint(cyl(0.0035, 0.004, at=(-0.048, 0, 0.180), seg=12,
                           rot=(0, math.radians(90), 0)), "dark"))           # nozzle tip
    parts.append(paint(wedge([(-0.006, -0.001), (0.006, -0.001), (0.009, -0.030), (-0.003, -0.032)],
                             0.020, at=(-0.024, -0.010, 0.170), rot=(math.radians(90), 0, 0),
                             name="_lever"), "white"))
    o = join(parts, "SM_RepellentBottle", bevel_width=0.0012)
    socket("Socket_Grip", at=(0, 0, 0.075), parent=o)
    return o, [o]


def drinking_vessel():
    """Enamelware cup with a blue rim, a footring, water inside and a D-shaped blue handle that
    actually stands off the side (the first version's handle was a ring lying flat against
    the wall). Reads as "clean drinking water" at arm's length."""
    prof = [(0.000, 0.000), (0.026, 0.000), (0.032, 0.003), (0.034, 0.008), (0.033, 0.014),
            (0.036, 0.060), (0.040, 0.094), (0.043, 0.101), (0.0435, 0.105), (0.042, 0.107),
            (0.039, 0.106), (0.037, 0.100), (0.034, 0.070), (0.031, 0.020), (0.026, 0.014),
            (0.000, 0.013)]
    cup = paint(revolve(prof, seg=40, name="_cup"), "enamel")
    paint_faces(cup, "enamel_blue", lambda c, n: c.z > 0.0985)
    parts = [cup]
    parts.append(paint(cyl(0.0355, 0.002, at=(0, 0, 0.090), seg=40), "water"))
    handle = torus(0.026, 0.0045, at=(0.045, 0, 0.060), rot=(math.radians(90), 0, 0), mseg=32, nseg=8)
    delete_verts(handle, lambda co: co.x < -0.004)          # the half that would be inside the cup
    parts.append(paint(handle, "enamel_blue"))
    o = join(parts, "SM_DrinkingVessel", bevel_width=0.0)
    socket("Socket_Grip", at=(0, 0, 0.060), parent=o)
    return o, [o]


def cloth():
    """A folded checked cloth (a pha khao ma, the everyday Lao cotton cloth): a rounded slab
    swept along its length with soft folds across the top and a little sag at the ends, its
    faces painted in two alternating tones for the checks."""
    L, Wd, T, cr = 0.40, 0.26, 0.046, 0.020
    NP, NY = 56, 24
    segs = [(L, 0), (T - cr, 1), (math.pi * cr / 2, 2), (L - 2 * cr, 3), (math.pi * cr / 2, 4), (T - cr, 5)]
    perimeter = sum(length for length, _ in segs)

    def outline(k):                                     # rounded top corners, flat bottom
        s = perimeter * k / NP
        for length, kind in segs:
            if s <= length or kind == 5:
                f = max(0.0, min(1.0, s / length))
                if kind == 0: return (-L / 2 + L * f, 0.0)
                if kind == 1: return (L / 2, (T - cr) * f)
                if kind == 2:
                    a = f * math.pi / 2
                    return (L / 2 - cr + cr * math.cos(a), T - cr + cr * math.sin(a))
                if kind == 3: return (L / 2 - cr - (L - 2 * cr) * f, T)
                if kind == 4:
                    a = math.pi / 2 + f * math.pi / 2
                    return (-L / 2 + cr + cr * math.cos(a), T - cr + cr * math.sin(a))
                return (-L / 2, (T - cr) * (1 - f))
            s -= length

    bm = bmesh.new()
    rings = []
    for iy in range(NY + 1):
        y = -Wd / 2 + Wd * iy / NY
        ring = []
        for k in range(NP):
            x, z = outline(k)
            if z > T * 0.55:                             # folds across the top, sag at the ends
                z += 0.004 * math.sin(2 * math.pi * x / 0.10 + 0.6) * math.cos(math.pi * y / Wd)
            z *= 1 - 0.05 * (2 * y / Wd) ** 2
            ring.append(bm.verts.new((x, y, z)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(NP):
            j = (k + 1) % NP
            bm.faces.new((a[k], b[k], b[j], a[j]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_mesh_obj(bm, "SM_Cloth")
    paint(o, "cloth_a")
    paint_faces(o, "cloth_b", lambda c, n: (math.floor((c.x + L / 2) / 0.05)
                                            + math.floor((c.y + Wd / 2) / 0.05)) % 2 == 1)
    o = join([o], "SM_Cloth", bevel_width=0.0)
    socket("Socket_Grip", at=(0, 0, 0.060), parent=o)
    return o, [o]


def torch():
    """Hand torch: lathed body with a knurled grip, a flared silver bezel around a pale lens,
    a red rubber button and a small D-ring on the tail. Stands upright on its base."""
    prof = [(0.000, 0.000), (0.019, 0.001), (0.023, 0.004), (0.024, 0.010), (0.021, 0.015)]
    for i in range(10):                                  # grip ribs
        z = 0.018 + i * 0.009
        prof += [(0.0205, z), (0.0228, z + 0.0045)]
    prof += [(0.0205, 0.108), (0.021, 0.116), (0.026, 0.126), (0.031, 0.138), (0.033, 0.148),
             (0.033, 0.160), (0.031, 0.165), (0.028, 0.165), (0.028, 0.161), (0.000, 0.160)]
    body = paint(revolve(prof, seg=36, name="_torch"), "dark")
    paint_faces(body, "silver", lambda c, n: c.z > 0.117)
    paint_faces(body, "lens", lambda c, n: c.z > 0.1595 and polar(c) < 0.0285)
    parts = [body]
    button = box((0.014, 0.008, 0.022), at=(0, -0.023, 0.098))
    apply_rotation(button)
    bevel(button, 0.003, 3)
    parts.append(paint(button, "red"))
    parts.append(paint(torus(0.008, 0.002, at=(0, 0.027, 0.009), rot=(0, math.radians(90), 0),
                             mseg=16, nseg=6), "silver"))                   # tail D-ring
    o = join(parts, "SM_Torch", bevel_width=0.0)      # the ribs would all be bevelled
    socket("Socket_Grip", at=(0, 0, 0.065), parent=o)
    socket("Socket_Light", at=(0, 0, 0.176), facing=(0, 0, 1), parent=o)
    return o, [o]


def health_centre():
    """PROVISIONAL massing (Sec.13 item 4 unresolved), but now a building: whitewashed walls on
    a concrete plinth under a real hipped roof in red sheeting, a porch on round columns,
    framed windows with sills on every side, a painted door and a sign with a red cross."""
    RIDGE_Z, EAVE_Z, WALL_Z = 3.65, 3.05, 3.04
    parts = []
    parts.append(paint(box((12.2, 8.2, 0.15), at=(0, 0, 0.075)), "concrete"))           # plinth
    parts.append(paint(box((12.0, 8.0, WALL_Z - 0.15), at=(0, 0, (WALL_Z + 0.15) / 2)), "whitewash"))

    # hipped roof: four slopes from the eave rectangle to a short ridge, thickened downward
    bm = bmesh.new()
    E = {(sx, sy): bm.verts.new((sx * 6.3, sy * 4.1, EAVE_Z)) for sx in (-1, 1) for sy in (-1, 1)}
    R = {sx: bm.verts.new((sx * 3.0, 0, RIDGE_Z)) for sx in (-1, 1)}
    bm.faces.new((E[(-1, -1)], E[(1, -1)], R[1], R[-1]))
    bm.faces.new((E[(1, 1)], E[(-1, 1)], R[-1], R[1]))
    bm.faces.new((E[(-1, 1)], E[(-1, -1)], R[-1]))
    bm.faces.new((E[(1, -1)], E[(1, 1)], R[1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    roof = new_mesh_obj(bm, "_roof")
    solidify(roof, 0.12, offset=-1)
    paint(roof, "roof")
    paint_faces(roof, "whitewash", lambda c, n: n.z < 0.5)              # soffit and fascia
    parts.append(roof)
    parts.append(paint(box((6.2, 0.30, 0.05), at=(0, 0, RIDGE_Z - 0.03)), "dark"))     # ridge cap

    # porch: a lean-to canopy on four columns over a slab
    bm = bmesh.new()
    a = [bm.verts.new((sx * 3.3, -4.0, 2.92)) for sx in (-1, 1)]
    b = [bm.verts.new((sx * 3.3, -6.2, 2.55)) for sx in (-1, 1)]
    bm.faces.new((b[0], b[1], a[1], a[0]))                          # wound so the normal points up
    canopy = new_mesh_obj(bm, "_canopy")
    solidify(canopy, 0.10, offset=-1)
    paint(canopy, "roof")
    paint_faces(canopy, "whitewash", lambda c, n: n.z < 0.5)
    parts.append(canopy)
    for cx in (-2.7, -0.9, 0.9, 2.7):
        col = revolve([(0, 0.15), (0.14, 0.15), (0.14, 0.28), (0.10, 0.34), (0.10, 2.40),
                       (0.13, 2.48), (0.13, 2.60), (0, 2.60)], seg=20, at=(cx, -5.95, 0), name="_col")
        parts.append(paint(col, "whitewash"))
    parts.append(paint(box((6.8, 2.2, 0.15), at=(0, -5.1, 0.075)), "concrete"))         # porch slab

    # door with a frame, on the front (-Y)
    parts.append(paint(box((1.50, 0.06, 2.05), at=(0, -4.02, 0.15 + 1.025)), "door"))
    for sx in (-1, 1):
        parts.append(paint(box((0.10, 0.08, 2.15), at=(sx * 0.80, -4.02, 0.15 + 1.075)), "whitewash"))
    parts.append(paint(box((1.70, 0.08, 0.10), at=(0, -4.02, 0.15 + 2.20)), "whitewash"))

    # windows: glass + frame + sill, on all four walls
    def window(cx, cy, along_x, out):                    # out: +1/-1, which way is outside
        gw, gh, zc = 1.20, 1.20, 1.60
        if along_x:                                          # in a wall running along X (front/back)
            parts.append(paint(box((gw, 0.04, gh), at=(cx, cy + out * 0.01, zc)), "glass"))
            for dx in (-1, 1):
                parts.append(paint(box((0.08, 0.07, gh + 0.16), at=(cx + dx * (gw / 2 + 0.04), cy + out * 0.02, zc)), "whitewash"))
            for dz in (-1, 1):
                parts.append(paint(box((gw + 0.16, 0.07, 0.08), at=(cx, cy + out * 0.02, zc + dz * (gh / 2 + 0.04))), "whitewash"))
            parts.append(paint(box((gw + 0.30, 0.10, 0.06), at=(cx, cy + out * 0.05, zc - gh / 2 - 0.11)), "whitewash"))
        else:                                                # in a gable wall running along Y
            parts.append(paint(box((0.04, gw, gh), at=(cx + out * 0.01, cy, zc)), "glass"))
            for dy in (-1, 1):
                parts.append(paint(box((0.07, 0.08, gh + 0.16), at=(cx + out * 0.02, cy + dy * (gw / 2 + 0.04), zc)), "whitewash"))
            for dz in (-1, 1):
                parts.append(paint(box((0.07, gw + 0.16, 0.08), at=(cx + out * 0.02, cy, zc + dz * (gh / 2 + 0.04))), "whitewash"))
            parts.append(paint(box((0.10, gw + 0.30, 0.06), at=(cx + out * 0.05, cy, zc - gh / 2 - 0.11)), "whitewash"))

    for cx in (-4.8, -2.8, 2.8, 4.8):
        window(cx, -4.0, True, -1)
    for cx in (-4.5, -1.5, 1.5, 4.5):
        window(cx, 4.0, True, 1)
    for sx in (-1, 1):
        for cy in (-1.6, 1.6):
            window(sx * 6.0, cy, False, sx)

    # sign board with a red cross, over the porch
    parts.append(paint(box((2.60, 0.08, 0.70), at=(0, -4.06, 2.62)), "whitewash"))
    parts.append(paint(box((0.44, 0.03, 0.14), at=(0, -4.115, 2.62)), "red"))
    parts.append(paint(box((0.14, 0.03, 0.44), at=(0, -4.115, 2.62)), "red"))

    o = join(parts, "SM_HealthCentre", bevel_width=0.0)
    socket("Socket_Entrance", at=(0, -6.6, 0), facing=(0, -1, 0), parent=o)
    return o, [o]


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------

ASSETS = [
    ("SM_ElectricFan",          electric_fan),
    ("SM_WindowScreen",         lambda: window_screen(False)),
    ("SM_WindowScreenTorn",     lambda: window_screen(True)),
    ("SM_MosquitoNet_RolledUp", mosquito_net_rolled_up),
    ("SM_RepellentBottle",      repellent_bottle),
    ("SM_DrinkingVessel",       drinking_vessel),
    ("SM_Cloth",                cloth),
    ("SM_Torch",                torch),
    ("SM_HealthCentre",         health_centre),
]


def export(root, name, out_dir):
    d = os.path.join(out_dir, name)
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, name + ".fbx")

    bpy.ops.object.select_all(action='DESELECT')
    def sel(o):
        o.select_set(True)
        for c in o.children:
            sel(c)
    sel(root)
    bpy.context.view_layer.objects.active = root

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        global_scale=1.0,
        axis_forward='-Y',
        axis_up='Z',
        object_types={'EMPTY', 'MESH'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode='AUTO',
    )
    return path


def main():
    argv = sys.argv
    out_dir = None
    if "--out" in argv:
        out_dir = argv[argv.index("--out") + 1]
    if not out_dir:
        here = os.path.dirname(os.path.abspath(__file__))
        repo = os.path.dirname(os.path.dirname(here))
        out_dir = os.path.join(repo, "Assets", "1.TeamWorkspace",
                               "Team Assets", "Models")
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None
    report, failures = [], []

    for name, build in ASSETS:
        if only and name not in only:
            continue
        reset()
        root, meshes = build()
        convert_to_yup()
        t = sum(tris(m) for m in meshes)
        budget = TRI_BUDGET[name]
        ok = t <= budget
        if not ok:
            failures.append(f"{name}: {t} tris over budget {budget}")
        pts = []
        for m in meshes:
            pts += [m.matrix_world @ Vector(c) for c in m.bound_box]
        mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        socks = sorted(o.name for o in bpy.data.objects if o.type == 'EMPTY')
        path = export(root, name, out_dir)
        report.append({
            "asset": name,
            "tris": t, "budget": budget, "within_budget": ok,
            "size_m_unity_yup": [round(v, 4) for v in (mx - mn)],
            "origin_offset": [round(v, 4) for v in mn],
            "parts": [m.name for m in meshes],
            "sockets": socks,
        })

    print("===REPORT===")
    print(json.dumps(report, indent=1))
    if failures:
        print("===BUDGET FAILURES===")
        for f in failures:
            print(" ", f)
        sys.exit(1)
    print("===OK=== %d assets" % len(report))


if __name__ == "__main__":
    main()
