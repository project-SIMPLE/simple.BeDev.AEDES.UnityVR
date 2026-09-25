#!/usr/bin/env blender --python
"""
Greybox placeholder models for Module 3 (AEDES).

Implements the "Unblocking pattern" of Documentation/Module3-Roadmap.md Sec.4:
every genuinely-missing asset gets a placeholder mesh at its FINAL path and name,
with the FINAL socket transforms, so code can bind to paths now and art can swap
the mesh inside the prefab later with no scene churn.

Run:
    blender -b --factory-startup --python Tools/Module3/make_placeholders.py

Everything here is deliberately primitive-built and parametric. These are
greybox stand-ins whose *dimensions and pivots* are the deliverable, not their
silhouettes. Numbers marked MEASURED were taken off the existing repo assets
with Tools/Module3/measure_reference.py and must not be changed casually --
the screens have to fit the Lao house window openings and the net variants have
to swap 1:1 with PF_MosquitoNet.

Prefab contract (roadmap Sec.4):
  FBX, metres, Y-up / -Z forward, transforms applied, one material per prop,
  sockets as empties named Socket_*, SM_ naming.
"""

import bpy, bmesh, math, os, sys, json
from mathutils import Vector, Matrix

# ---------------------------------------------------------------------------
# Reference dimensions MEASURED from existing repo assets
# ---------------------------------------------------------------------------
# SM_Windows/SM_WindowRight.fbx + SM_WindowLeft.fbx: two 0.75 x 1.00 m leaves
# meeting on the centre line, frame reveal 0.1202 m deep.
WINDOW_W        = 1.50   # MEASURED full opening width
WINDOW_H        = 1.00   # MEASURED full opening height
WINDOW_REVEAL   = 0.12   # MEASURED reveal depth the screen must sit inside

# SM_MosquitoNet/SM_MosquitoNet.fbx: dome 1.5596 x 1.7661 footprint, 1.391 tall,
# base on z=0, origin centred in plan. The rolled-up variant must share that
# origin so the two prefabs are drop-in swaps at one transform.
NET_TOP         = 1.39   # MEASURED deployed canopy height

# SM_Bed/SM_Bed.fbx: 1.5961 x 1.9368 x 1.1807 -- kept for reference; the net
# is sized off the net, not the bed.
BED_W, BED_L    = 1.60, 1.94

TRI_BUDGET = {           # roadmap Sec.4 "Budget"
    "SM_ElectricFan":          3000,
    "SM_WindowScreen":          500,
    "SM_WindowScreenTorn":      500,
    "SM_MosquitoNet_RolledUp": 1500,
    "SM_RepellentBottle":      1500,
    "SM_DrinkingVessel":       1500,
    "SM_Cloth":                1500,
    "SM_Torch":                1500,
    # Not in the contract table (it is a building, and Sec.13 item 4 has not
    # resolved whether it is even needed). Kept crude on purpose.
    "SM_HealthCentre":         2000,
}

MATERIAL = "M_Module3Placeholder"

# ---------------------------------------------------------------------------
# Small primitive helpers. All build in Blender's Z-up metres; the FBX export
# converts to Y-up / -Z forward at the end.
# ---------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0

def _added():
    return bpy.context.active_object

def box(size, at=(0, 0, 0), rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at, rotation=rot)
    o = _added()
    o.scale = size
    return o

def cyl(r, h, at=(0, 0, 0), seg=16, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=seg,
                                        location=at, rotation=rot)
    return _added()

def frustum(r1, r2, h, at=(0, 0, 0), seg=16, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=h,
                                    vertices=seg, location=at, rotation=rot)
    return _added()

def ring(major, minor, at=(0, 0, 0), rot=(0, 0, 0), mseg=16, nseg=6):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=mseg, minor_segments=nseg,
                                     location=at, rotation=rot)
    return _added()

def join(parts, name):
    """Join parts into one mesh object, apply all transforms, name it."""
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    # "all transforms applied" -- bake rotation/scale/location into the mesh.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.name = name
    o.data.name = name
    return o

def shade_flat(o):
    for p in o.data.polygons:
        p.use_smooth = False

def material(o):
    """One material per prop (roadmap Sec.4)."""
    m = bpy.data.materials.get(MATERIAL) or bpy.data.materials.new(MATERIAL)
    m.diffuse_color = (0.62, 0.62, 0.60, 1.0)
    o.data.materials.clear()
    o.data.materials.append(m)

def socket(name, at=(0, 0, 0), facing=None, parent=None):
    """An empty marking an attachment point. The local offset IS the data.

    `facing` is a direction in this script's authoring frame (Z-up). The socket
    is rotated so its local +Z points that way, because after the Y-up
    conversion below, local +Z becomes Unity's forward. So a Light or a player
    spawn parented to the socket with identity rotation faces the right way.
    Sockets with no facing keep identity rotation.
    """
    bpy.ops.object.empty_add(type='PLAIN_AXES', radius=0.05, location=at)
    e = _added()
    e.name = name
    if facing is not None:
        f = Vector(facing).normalized()
        e.rotation_euler = f.to_track_quat('Z', 'Y').to_euler()
        e["m3_facing"] = list(f)        # re-derived by convert_to_yup()
    if parent:
        e.parent = parent
        # Parents here sit at the origin with identity rotation, so the parent
        # inverse is identity; being explicit keeps the local offset readable.
        e.matrix_parent_inverse = Matrix.Identity(4)
    return e


# ---------------------------------------------------------------------------
# Z-up (Blender) -> Y-up (Unity) conversion, done explicitly.
#
# The obvious route -- let the FBX exporter convert via axis_up='Y' -- does not
# work cleanly here:
#   * bake_space_transform=False leaves the conversion as a -90deg X rotation on
#     the prefab ROOT. The prefab looks upright, but "all transforms applied"
#     (contract, Sec.4) is false, and anything that zeroes, re-parents or drives
#     the root rotation tips the object over.
#   * bake_space_transform=True bakes it into vertex data but mangles PARENTED
#     hierarchies -- it displaced Fan_Blade and inflated the fan's bounds from
#     0.306 x 0.453 x 0.21 to 0.306 x 0.503 x 0.531.
#
# So the conversion is applied here instead, and the FBX is then exported with
# the conversion disabled. Every object ends up with identity rotation, unit
# scale, and mesh data already in Unity's axes. Because each parent sits at the
# origin with identity rotation, a child's local offset lives in the same frame
# as its parent's, so rotating locations and mesh data by R is exact.
# ---------------------------------------------------------------------------

YUP = Matrix.Rotation(math.radians(-90), 4, 'X')   # Blender +Z up -> Unity +Y up


def convert_to_yup():
    """Rotate every object's mesh data and local offset into Unity's axes.

    Meshes keep identity rotation: the rotation goes into the vertices. A socket
    that encodes a direction has its facing re-derived in the new frame; a socket
    that does not stays identity, so its axes line up with its parent's.
    """
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

# ---------------------------------------------------------------------------
# Assets
# ---------------------------------------------------------------------------

def electric_fan():
    """Table/floor fan. Sec.6 calls this out as the realistic alternative where
    there is no screen, so the blade has to actually turn: Fan_Blade is a
    separate object whose origin sits exactly on the spin axis.

    Spin axis: Blender +Y, which the FBX axis conversion turns into Unity +Z.
    So in Unity the animator spins Fan_Blade around its LOCAL Z.
    """
    parts = []
    parts.append(cyl(0.105, 0.030, at=(0, 0, 0.015), seg=16))          # base disc
    parts.append(cyl(0.022, 0.200, at=(0, 0, 0.130), seg=12))          # column
    parts.append(box((0.055, 0.055, 0.045), at=(0, 0, 0.250)))         # yoke
    base = join(parts, "Fan_Base")
    shade_flat(base); material(base)

    HUB_Z = 0.300           # height of the spin axis
    parts = []
    # motor housing, behind the blade (+Y is "back", the fan faces -Y)
    parts.append(cyl(0.055, 0.090, at=(0, 0.045, HUB_Z), seg=12,
                     rot=(math.radians(90), 0, 0)))
    # guard: two rings either side of the blade plane
    parts.append(ring(0.145, 0.008, at=(0, -0.075, HUB_Z),
                      rot=(math.radians(90), 0, 0), mseg=16, nseg=6))
    parts.append(ring(0.145, 0.008, at=(0, 0.005, HUB_Z),
                      rot=(math.radians(90), 0, 0), mseg=16, nseg=6))
    for i in range(4):                                                 # spokes
        a = math.radians(45 + i * 90)
        parts.append(box((0.010, 0.008, 0.290), at=(0, -0.035, HUB_Z),
                         rot=(0, a, 0)))
    head = join(parts, "Fan_Head")
    shade_flat(head); material(head)

    # Blade: authored around the world origin so its own origin is the pivot,
    # then offset by location only. Rotation and scale stay identity.
    parts = []
    parts.append(cyl(0.022, 0.030, at=(0, 0, 0), seg=10,
                     rot=(math.radians(90), 0, 0)))                    # hub
    for i in range(3):
        a = math.radians(i * 120)
        b = box((0.115, 0.006, 0.055), at=(0, 0, 0))
        # push outward along local X, then twist for pitch, then space 120 deg
        b.location = (0.075 * math.cos(a), 0.0, 0.075 * math.sin(a))
        b.rotation_euler = (0, -a + math.radians(20), 0)
        # re-place after rotation so the blade root meets the hub
        b.location = (0.075 * math.cos(a), 0.0, 0.075 * math.sin(a))
        parts.append(b)
    blade = join(parts, "Fan_Blade")
    shade_flat(blade); material(blade)

    blade.parent = head
    blade.location = (0, -0.035, HUB_Z)      # pivot on the spin axis
    head.parent = base

    socket("Socket_Mount", at=(0, 0, 0), parent=base)
    return base, [base, head, blade]


def window_screen(torn=False):
    """Insect screen for the existing Lao house window opening.

    Origin: bottom-centre of the opening, so it drops onto a sill centre line.
    Depth is kept inside the MEASURED 0.12 m reveal.
    """
    fw, d = 0.035, 0.040                      # frame section, total depth
    W, H = WINDOW_W, WINDOW_H
    assert d <= WINDOW_REVEAL, "screen deeper than the measured window reveal"

    parts = []
    parts.append(box((W, d, fw), at=(0, 0, fw / 2)))            # bottom rail
    parts.append(box((W, d, fw), at=(0, 0, H - fw / 2)))        # top rail
    parts.append(box((fw, d, H - 2 * fw), at=(-(W - fw) / 2, 0, H / 2)))   # stile
    parts.append(box((fw, d, H - 2 * fw), at=((W - fw) / 2, 0, H / 2)))    # stile

    iw, ih = W - 2 * fw, H - 2 * fw           # inner opening the mesh spans
    if not torn:
        parts.append(box((iw, 0.006, ih), at=(0, 0, H / 2)))    # intact mesh
        name = "SM_WindowScreen"
    else:
        # A tear in the lower outboard corner: the mesh survives as an L, with
        # two flaps peeled into the hole. This is the variant the player repairs.
        hole_w, hole_h = iw * 0.42, ih * 0.38
        parts.append(box((iw, 0.006, ih - hole_h),
                         at=(0, 0, H / 2 + hole_h / 2)))                  # upper band
        parts.append(box((iw - hole_w, 0.006, hole_h),
                         at=(-hole_w / 2, 0, H / 2 - (ih - hole_h) / 2)))  # lower-left
        parts.append(box((hole_w * 0.55, 0.005, hole_h * 0.5),
                         at=(iw / 2 - hole_w * 0.30, -0.012,
                             H / 2 - (ih - hole_h) / 2 + hole_h * 0.22),
                         rot=(math.radians(-18), 0, math.radians(7))))     # flap
        parts.append(box((hole_w * 0.40, 0.005, hole_h * 0.36),
                         at=(iw / 2 - hole_w * 0.62, -0.008,
                             H / 2 - (ih - hole_h) / 2 - hole_h * 0.18),
                         rot=(math.radians(14), 0, math.radians(-10))))    # flap
        name = "SM_WindowScreenTorn"

    o = join(parts, name)
    shade_flat(o); material(o)
    socket("Socket_Mount", at=(0, 0, 0), parent=o)
    return o, [o]


def mosquito_net_rolled_up():
    """The "up" state of PF_MosquitoNet (roadmap Sec.4).

    The deployed state reuses the existing SM_MosquitoNet -- no new mesh for
    that. This is the gathered bundle tied up out of the way. Origin sits on
    z=0 like the deployed net so the two prefabs swap at one transform.
    """
    parts = []
    parts.append(frustum(0.10, 0.16, 0.48, at=(0, 0, 1.06), seg=12))   # bundle
    parts.append(ring(0.10, 0.022, at=(0, 0, 0.82), mseg=12, nseg=6))  # hem
    parts.append(cyl(0.060, 0.090, at=(0, 0, NET_TOP - 0.045), seg=10))  # gather
    o = join(parts, "SM_MosquitoNet_RolledUp")
    shade_flat(o); material(o)
    socket("Socket_Mount", at=(0, 0, 0), parent=o)
    socket("Socket_Hook", at=(0, 0, NET_TOP), parent=o)
    return o, [o]


def repellent_bottle():
    parts = []
    parts.append(cyl(0.030, 0.115, at=(0, 0, 0.0575), seg=14))
    parts.append(frustum(0.030, 0.018, 0.030, at=(0, 0, 0.130), seg=14))
    parts.append(cyl(0.012, 0.016, at=(0, 0, 0.153), seg=10))
    parts.append(box((0.050, 0.032, 0.030), at=(-0.010, 0, 0.176)))       # head
    parts.append(cyl(0.006, 0.020, at=(-0.032, 0, 0.180), seg=8,
                     rot=(0, math.radians(90), 0)))                        # nozzle
    o = join(parts, "SM_RepellentBottle")
    shade_flat(o); material(o)
    socket("Socket_Grip", at=(0, 0, 0.075), parent=o)
    return o, [o]


def drinking_vessel():
    """Sec.4 "drinking vessel / water bottle" -- for the "bring water" action."""
    parts = []
    parts.append(frustum(0.032, 0.042, 0.110, at=(0, 0, 0.055), seg=16))
    parts.append(cyl(0.038, 0.004, at=(0, 0, 0.100), seg=16))   # water surface
    o = join(parts, "SM_DrinkingVessel")
    shade_flat(o); material(o)
    socket("Socket_Grip", at=(0, 0, 0.060), parent=o)
    return o, [o]


def cloth():
    """Folded cloth/towel -- one of the Sec.4 props that dresses an unwell
    villager without flagging them (Sec.5 framing rule)."""
    parts = []
    parts.append(box((0.400, 0.260, 0.022), at=(0.000, 0, 0.011)))
    parts.append(box((0.385, 0.245, 0.020), at=(0.006, 0, 0.032)))
    parts.append(box((0.370, 0.230, 0.018), at=(-0.005, 0, 0.051)))
    o = join(parts, "SM_Cloth")
    shade_flat(o); material(o)
    socket("Socket_Grip", at=(0, 0, 0.060), parent=o)
    return o, [o]


def torch():
    """Hand torch. Stands upright so it can sit on a shelf; the hand uses
    Socket_Grip. Socket_Light sits just outside the lens and its local -Y
    points along the beam, which the axis conversion turns into Unity +Z --
    so a Light parented to it with identity rotation shines down the beam.
    """
    parts = []
    parts.append(cyl(0.021, 0.130, at=(0, 0, 0.065), seg=14))          # body
    parts.append(cyl(0.023, 0.008, at=(0, 0, 0.004), seg=14))          # tail cap
    parts.append(frustum(0.021, 0.033, 0.035, at=(0, 0, 0.1475), seg=14))
    parts.append(cyl(0.031, 0.006, at=(0, 0, 0.168), seg=14))          # lens
    o = join(parts, "SM_Torch")
    shade_flat(o); material(o)
    socket("Socket_Grip", at=(0, 0, 0.065), parent=o)
    # +Z here is up, which is where an upright torch points its beam.
    socket("Socket_Light", at=(0, 0, 0.176), facing=(0, 0, 1), parent=o)
    return o, [o]


def health_centre():
    """PROVISIONAL massing only. Sec.13 item 4 has not resolved whether the
    player travels to the health centre; referral is modelled as an off-screen
    outcome first. This exists so a travel loop can be greyboxed the day that
    question is answered, and is scheduled last on purpose.
    """
    parts = []
    parts.append(box((12.0, 8.0, 3.40), at=(0, 0, 1.70)))              # block
    parts.append(box((12.6, 8.6, 0.25), at=(0, 0, 3.525)))             # roof
    parts.append(box((4.0, 2.0, 0.18), at=(0, -5.0, 2.70)))            # canopy
    parts.append(box((0.16, 0.16, 2.70), at=(-1.8, -5.9, 1.35)))       # post
    parts.append(box((0.16, 0.16, 2.70), at=(1.8, -5.9, 1.35)))        # post
    parts.append(box((1.60, 0.15, 2.20), at=(0, -4.02, 1.10)))         # door
    parts.append(box((2.40, 0.10, 0.70), at=(0, -4.06, 2.95)))         # sign
    o = join(parts, "SM_HealthCentre")
    shade_flat(o); material(o)
    # The porch is on -Y, so the entrance faces -Y: a player arriving along the
    # socket's forward walks in through the door.
    socket("Socket_Entrance", at=(0, -5.6, 0), facing=(0, -1, 0), parent=o)
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
    """One FBX per asset, into Team Assets/Models/<name>/<name>.fbx, matching
    the existing folder-per-model convention."""
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
        # FBX_SCALE_UNITS puts the metre->centimetre unit scaling into the FBX
        # header's UnitScaleFactor (100.0) and leaves object transforms in
        # metres. This is what the repo's existing art declares, and it is what
        # Unity's importer expects with useFileUnits/useFileScale enabled.
        # With FBX_SCALE_NONE the header says 1.0 and Unity scales everything
        # down by 100x -- and a Blender round-trip will NOT catch it, because
        # Blender reads its own convention back consistently. Verify in Unity.
        apply_scale_options='FBX_SCALE_UNITS',
        global_scale=1.0,
        # The data is ALREADY in Unity's axes (see convert_to_yup), so the
        # exporter must not convert again: passing Blender's own native axes
        # makes its conversion matrix the identity. The FBX still satisfies the
        # contract's "Y-up / -Z forward" -- that is what the geometry now is.
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
    report, failures = [], []

    for name, build in ASSETS:
        reset()
        root, meshes = build()
        convert_to_yup()
        t = sum(tris(m) for m in meshes)
        budget = TRI_BUDGET[name]
        ok = t <= budget
        if not ok:
            failures.append(f"{name}: {t} tris over budget {budget}")
        # bounds, world space, across the mesh parts
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
            "fbx": os.path.relpath(path, os.path.dirname(os.path.dirname(
                   os.path.dirname(os.path.dirname(os.path.dirname(path)))))),
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
