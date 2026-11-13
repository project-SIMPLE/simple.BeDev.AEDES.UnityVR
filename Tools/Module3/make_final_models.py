#!/usr/bin/env blender --python
"""
Final art for the Module 3 (AEDES) asset gap, replacing the greybox
placeholders from Tools/Module3/make_placeholders.py.

Per Documentation/Module3-Placeholder-Assets.md "Handing these to whoever
makes the real models": this keeps the same node names (Fan_Base / Fan_Head /
Fan_Blade), the same Socket_* empties at the same points, the same overall
bounding box (Module3PlaceholderPrefabs.cs Verify checks size to +/-2cm) and
the same triangle budgets -- only the silhouette inside that envelope changes.
Exporting over the same Team Assets/Models/<name>/<name>.fbx path, with the
existing .fbx.meta left untouched, is what makes this a one-file swap with no
scene or prefab-path churn.

Run:
    blender -b --factory-startup --python Tools/Module3/make_final_models.py

Format is FBX, not glTF: Documentation/Module3-Asset-Brief.md Sec.2 is explicit
that glTF/.glb is accepted for placeholders only, and this project's whole
import pipeline (materialImportMode, stable GUIDs, the prefab builder) is
built around FBX. The axis/unit conversion below is copied verbatim from
make_placeholders.py -- see Module3-Placeholder-Assets.md "Axis and unit
traps" for why it is done this way and not left to the exporter.
"""

import bpy, bmesh, math, os, sys, json
from mathutils import Vector, Matrix

# ---------------------------------------------------------------------------
# Reference dimensions MEASURED from existing repo assets (unchanged from
# make_placeholders.py -- these fit the shipped house and bed, not invented).
# ---------------------------------------------------------------------------
WINDOW_W        = 1.50   # SM_WindowLeft/Right: two 0.75 m leaves, full opening
WINDOW_H        = 1.00
WINDOW_REVEAL   = 0.12   # frame reveal depth the screen must sit inside
NET_TOP         = 1.39   # SM_MosquitoNet canopy height -- rolled net shares it

TRI_BUDGET = {
    "SM_ElectricFan":          3000,
    "SM_WindowScreen":          500,
    "SM_WindowScreenTorn":      500,
    "SM_MosquitoNet_RolledUp": 1500,
    "SM_RepellentBottle":      1500,
    "SM_DrinkingVessel":       1500,
    "SM_Cloth":                1500,
    "SM_Torch":                1500,
    "SM_HealthCentre":         2000,
}

# One flat colour per asset (Asset Brief Sec.2: "one material per asset, not
# one per part"). Unity ignores whatever material the FBX carries --
# make_unity_assets.py sets materialImportMode=0 -- so this is only a preview
# aid here; the real .mat files are written by make_final_materials.py with
# these same colours, so what you see in Blender is what ships.
COLOR = {
    "SM_ElectricFan":          (0.88, 0.86, 0.80),   # cream plastic
    "SM_WindowScreen":         (0.40, 0.33, 0.24),   # weathered wood + mesh
    "SM_WindowScreenTorn":     (0.40, 0.33, 0.24),   # brief: same material as intact
    "SM_MosquitoNet_RolledUp":(0.90, 0.88, 0.82),    # cream netting fabric
    "SM_RepellentBottle":     (0.82, 0.52, 0.16),    # amber plastic, no brand
    "SM_DrinkingVessel":      (0.87, 0.85, 0.79),    # enamelware off-white
    "SM_Cloth":               (0.52, 0.68, 0.66),    # calm cloth colour
    "SM_Torch":               (0.11, 0.11, 0.12),    # black plastic body
    "SM_HealthCentre":        (0.90, 0.88, 0.82),    # whitewashed masonry
}

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

def ring(major, minor, at=(0, 0, 0), rot=(0, 0, 0), mseg=20, nseg=8):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=mseg, minor_segments=nseg,
                                     location=at, rotation=rot)
    return _added()

def lathe(profile, seg=20, at=(0, 0, 0), name="Lathe"):
    """Surface of revolution around local Z from a (radius, z) profile, bottom
    to top. A profile endpoint at radius 0 closes as a pole -- that is the
    standard way to cap a lathed body (a bottle base, a dome tip) without a
    separate cap object.
    """
    bm = bmesh.new()
    verts = [bm.verts.new((r, 0.0, z)) for r, z in profile]
    edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
    bmesh.ops.spin(bm, geom=verts + edges, axis=(0, 0, 1), cent=(0, 0, 0),
                    angle=math.radians(360), steps=seg, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = at
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    return o

def wedge(pts_bottom, thickness, at=(0, 0, 0), rot=(0, 0, 0), name="Wedge"):
    """An extruded flat polygon (a list of (x, y) points, CCW) -- used for
    blades, flaps and leaf-shaped props where a box would look wrong.
    """
    bm = bmesh.new()
    bverts = [bm.verts.new((x, y, 0.0)) for x, y in pts_bottom]
    bm.faces.new(bverts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    r = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, vec=(0, 0, thickness),
                         verts=[v for v in r['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = at
    o.rotation_euler = rot
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=False)
    return o

def join(parts, name, bevel_width=0.0015, bevel_seg=1):
    """Join parts into one mesh, apply a small edge bevel for a toon-friendly
    highlight instead of razor-sharp primitive edges, apply all transforms,
    and name it.
    """
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel_width > 0:
        mod = o.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel_width
        mod.segments = bevel_seg
        mod.limit_method = 'ANGLE'
        mod.angle_limit = math.radians(35)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    o.name = name
    o.data.name = name
    return o

def shade_flat(o):
    for p in o.data.polygons:
        p.use_smooth = False

def shade_smooth(o):
    for p in o.data.polygons:
        p.use_smooth = True

def material(o, key):
    """One material per prop (Asset Brief Sec.2), named after the asset like
    the rest of the project's M_ convention. Unity does not read this colour
    back (materialImportMode=0) -- make_final_materials.py is the real source
    for the shipped colour -- but keeping them identical means the Blender
    preview render matches what ships.
    """
    mname = "M_" + key[3:] if key.startswith("SM_") else "M_" + key
    m = bpy.data.materials.get(mname) or bpy.data.materials.new(mname)
    r, g, b = COLOR[key]
    m.diffuse_color = (r, g, b, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.6
    o.data.materials.clear()
    o.data.materials.append(m)

def socket(name, at=(0, 0, 0), facing=None, parent=None):
    """An empty marking an attachment point. See make_placeholders.py's
    docstring for why `facing` is expressed in this authoring frame and
    re-derived by convert_to_yup() below -- unchanged from that script.
    """
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

# ---------------------------------------------------------------------------
# Assets
# ---------------------------------------------------------------------------

def electric_fan():
    """Table/floor fan. Envelope, HUB_Z, guard radius and blade root/tip radii
    are held to the values verified in Unity (0.306 x 0.453 x 0.21) -- only
    the silhouette inside that envelope is new: a lathed foot-to-neck pedestal
    instead of a disc-and-tube, a rounded motor housing, a tilt knob, and
    tapered twisted blades instead of flat boxes.
    """
    HUB_Z = 0.300

    # Foot + column + neck as one lathed profile (r, z), bottom to top.
    pedestal_profile = [
        (0.000, 0.000),
        (0.105, 0.006),
        (0.105, 0.020),
        (0.084, 0.026),
        (0.034, 0.034),
        (0.026, 0.046),
        (0.026, 0.185),
        (0.036, 0.205),
        (0.036, 0.222),
        (0.000, 0.232),
    ]
    pedestal = lathe(pedestal_profile, seg=16, name="_ped")
    shade_smooth(pedestal)
    parts = [pedestal]
    parts.append(box((0.052, 0.052, 0.040), at=(0, 0, 0.238)))            # yoke block
    parts.append(cyl(0.012, 0.052, at=(0.030, 0, 0.238), seg=10,
                     rot=(0, math.radians(90), 0)))                        # tilt pivot pin
    base = join(parts, "Fan_Base")
    material(base, "SM_ElectricFan")

    # Motor housing: a short lathed drum with a domed front cap, instead of a
    # bare cylinder.
    housing_profile = [
        (0.000, -0.058),
        (0.050, -0.056),
        (0.058, -0.030),
        (0.058, 0.010),
        (0.048, 0.028),
        (0.000, 0.034),
    ]
    housing = lathe(housing_profile, seg=14, at=(0, 0.045, HUB_Z),
                    name="_housing")
    housing.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    shade_smooth(housing)
    parts = [housing]
    parts.append(cyl(0.010, 0.030, at=(0, -0.010, HUB_Z), seg=10,
                     rot=(math.radians(90), 0, 0)))                        # hub nub, front
    # Guard: two rings either side of the blade plane -- crossed rings per the
    # brief, not a wire mesh. Rings are built flat in XY then stood up by a
    # single X-axis rotation, so their big diameter reads in width/height and
    # only their thin tube adds to depth.
    parts.append(ring(0.145, 0.007, at=(0, -0.078, HUB_Z),
                      rot=(math.radians(90), 0, 0), mseg=18, nseg=6))
    parts.append(ring(0.145, 0.007, at=(0, 0.006, HUB_Z),
                      rot=(math.radians(90), 0, 0), mseg=18, nseg=6))
    for i in range(4):
        a = math.radians(45 + i * 90)
        parts.append(box((0.008, 0.006, 0.290), at=(0, -0.036, HUB_Z),
                         rot=(0, a, 0)))
    # speed-dial knob on the pedestal neck, small but reads well up close
    parts.append(cyl(0.014, 0.010, at=(0.026, 0.0, 0.150), seg=10,
                     rot=(0, math.radians(90), 0)))
    head = join(parts, "Fan_Head")
    material(head, "SM_ElectricFan")

    # Blade: three tapered leaves with a rounded tip, built as a flat polygon
    # in the disc plane (Blender X-Z, which becomes Unity's width-height) and
    # thin along the spin axis (Blender Y / Unity Z). Two single-axis
    # rotations -- stand the flat-XY wedge up into the X-Z plane, then a pure
    # Y-axis turn to space the three blades -- avoid the Euler-order trap of
    # combining pitch and spacing in one rotation, which previously smeared
    # the whole 0.132 m blade radius into the depth axis.
    hub = cyl(0.022, 0.032, at=(0, 0, 0), seg=12, rot=(math.radians(90), 0, 0))
    leaf_pts = [(0.075, -0.020), (0.100, -0.026), (0.132, -0.010),
                (0.132, 0.012), (0.098, 0.024), (0.075, 0.018)]
    parts = [hub]
    for i in range(3):
        a = math.radians(i * 120)
        leaf = wedge(leaf_pts, 0.006, at=(0, -0.003, 0), name=f"_leaf{i}")
        leaf.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        leaf.rotation_euler = (0, a, 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        parts.append(leaf)
    blade = join(parts, "Fan_Blade", bevel_width=0.0006)
    shade_smooth(blade)
    material(blade, "SM_ElectricFan")

    blade.parent = head
    blade.location = (0, -0.036, HUB_Z)
    head.parent = base

    socket("Socket_Mount", at=(0, 0, 0), parent=base)
    return base, [base, head, blade]


def window_screen(torn=False):
    """Insect screen for the Lao house window opening. A mullion crossbar and
    corner brackets replace the plain four-rail frame; the frame section is
    lathed-free (still boxes, since a rectangular frame IS the right shape)
    but bevelled for a real edge highlight.
    """
    fw, d = 0.032, 0.040
    W, H = WINDOW_W, WINDOW_H
    assert d <= WINDOW_REVEAL, "screen deeper than the measured window reveal"

    parts = []
    parts.append(box((W, d, fw), at=(0, 0, fw / 2)))                        # bottom rail
    parts.append(box((W, d, fw), at=(0, 0, H - fw / 2)))                    # top rail
    parts.append(box((fw, d, H - 2 * fw), at=(-(W - fw) / 2, 0, H / 2)))    # left stile
    parts.append(box((fw, d, H - 2 * fw), at=((W - fw) / 2, 0, H / 2)))     # right stile
    parts.append(box((fw * 0.8, d * 0.85, H - 2 * fw), at=(0, 0, H / 2)))   # centre mullion

    iw = (W - 2 * fw) / 2 - fw * 0.4          # inner opening per mullion bay
    ih = H - 2 * fw
    if not torn:
        for cx in (-(W / 4), W / 4):
            parts.append(box((iw, 0.006, ih), at=(cx, 0, H / 2)))           # intact mesh, 2 bays
        name = "SM_WindowScreen"
    else:
        hole_w, hole_h = iw * 0.58, ih * 0.40
        bay_cx = W / 4
        parts.append(box((iw, 0.006, iw), at=(-W / 4, 0, H / 2)))           # left bay, intact
        parts.append(box((iw, 0.006, ih - hole_h),
                         at=(bay_cx, 0, H / 2 + hole_h / 2)))                # right bay, upper band
        parts.append(box((iw - hole_w, 0.006, hole_h),
                         at=(bay_cx + hole_w / 2, 0, H / 2 - (ih - hole_h) / 2)))  # lower remainder
        parts.append(box((hole_w * 0.55, 0.005, hole_h * 0.5),
                         at=(bay_cx - iw / 2 + hole_w * 0.30, -0.014,
                             H / 2 - (ih - hole_h) / 2 + hole_h * 0.20),
                         rot=(math.radians(-22), 0, math.radians(8))))       # peeled flap
        parts.append(box((hole_w * 0.42, 0.005, hole_h * 0.40),
                         at=(bay_cx - iw / 2 + hole_w * 0.62, -0.010,
                             H / 2 - (ih - hole_h) / 2 - hole_h * 0.18),
                         rot=(math.radians(16), 0, math.radians(-11))))      # second flap
        name = "SM_WindowScreenTorn"

    o = join(parts, name, bevel_width=0.001)
    shade_flat(o)
    material(o, "SM_WindowScreen")
    socket("Socket_Mount", at=(0, 0, 0), parent=o)
    return o, [o]


def mosquito_net_rolled_up():
    """The "up" state of PF_MosquitoNet. A lathed, gathered-fabric bundle (a
    few uneven lumps rather than a clean frustum) with a wrapped tie cord,
    sharing the deployed net's origin and canopy height so the two prefabs
    swap at one transform.
    """
    profile = [
        (0.000, 0.800),
        (0.100, 0.815),
        (0.160, 0.880),
        (0.130, 0.950),
        (0.155, 1.030),
        (0.125, 1.110),
        (0.145, 1.190),
        (0.070, NET_TOP - 0.040),
        (0.030, NET_TOP - 0.008),
        (0.000, NET_TOP),
    ]
    bundle = lathe(profile, seg=14, name="SM_MosquitoNet_RolledUp")
    shade_smooth(bundle)
    parts = [bundle]
    # tie cord: two wraps at different heights, plus a base hem
    for z in (0.880, 1.110):
        parts.append(ring(0.140, 0.008, at=(0, 0, z), mseg=14, nseg=5))
    parts.append(ring(0.110, 0.016, at=(0, 0, 0.815), mseg=12, nseg=5))     # base hem
    o = join(parts, "SM_MosquitoNet_RolledUp", bevel_width=0.0008)
    shade_smooth(o)
    material(o, "SM_MosquitoNet_RolledUp")
    socket("Socket_Mount", at=(0, 0, 0), parent=o)
    socket("Socket_Hook", at=(0, 0, NET_TOP), parent=o)
    return o, [o]


def repellent_bottle():
    """Small trigger-spray bottle. Lathed body instead of a stacked-cylinder
    silhouette, plus a trigger lever and a nozzle -- generic, no brand marks.
    """
    profile = [
        (0.000, 0.000),
        (0.028, 0.003),
        (0.030, 0.014),
        (0.030, 0.098),
        (0.026, 0.112),
        (0.017, 0.122),
        (0.017, 0.142),
        (0.011, 0.148),
        (0.011, 0.160),
    ]
    body = lathe(profile, seg=16, name="_body")
    shade_smooth(body)
    parts = [body]
    parts.append(box((0.052, 0.034, 0.032), at=(-0.012, 0, 0.176)))         # trigger head
    parts.append(cyl(0.006, 0.022, at=(-0.036, 0, 0.180), seg=8,
                     rot=(0, math.radians(90), 0)))                         # nozzle
    lever = wedge([(-0.006, -0.001), (0.006, -0.001), (0.009, -0.030),
                   (-0.003, -0.032)], 0.020,
                  at=(-0.024, -0.010, 0.170), rot=(math.radians(90), 0, 0),
                  name="_lever")
    parts.append(lever)
    o = join(parts, "SM_RepellentBottle")
    shade_smooth(o)
    material(o, "SM_RepellentBottle")
    socket("Socket_Grip", at=(0, 0, 0.075), parent=o)
    return o, [o]


def drinking_vessel():
    """Enamelware cup with a rim lip, a footring and a handle -- "clean
    drinking water" read at arm's length, per the brief.
    """
    profile = [
        (0.000, 0.000),
        (0.030, 0.002),
        (0.034, 0.006),
        (0.032, 0.014),
        (0.036, 0.090),
        (0.042, 0.098),
        (0.043, 0.104),
        (0.039, 0.106),
    ]
    body = lathe(profile, seg=18, name="_cup")
    shade_smooth(body)
    parts = [body]
    parts.append(cyl(0.038, 0.003, at=(0, 0, 0.096), seg=18))               # water surface
    handle = ring(0.024, 0.0045, at=(0.046, 0, 0.058),
                 rot=(0, math.radians(90), 0), mseg=16, nseg=6)
    handle.scale = (1.0, 1.0, 0.7)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(handle)
    o = join(parts, "SM_DrinkingVessel")
    shade_smooth(o)
    material(o, "SM_DrinkingVessel")
    socket("Socket_Grip", at=(0, 0, 0.060), parent=o)
    return o, [o]


def cloth():
    """Folded cloth with rounded, sagging fold edges rather than three flat
    boxes -- built as a subdivided slab bent along its length so each fold
    reads as fabric, not a stack of boards.
    """
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = _added()
    o.scale = (0.400, 0.260, 0.046)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.mesh.select_mode(type='FACE')
    bm_sel = bmesh.from_edit_mesh(o.data)
    for f in bm_sel.faces:
        if abs(f.normal.z) > 0.9:          # only the large top/bottom faces
            f.select = True
    bmesh.update_edit_mesh(o.data)
    bpy.ops.mesh.subdivide(number_cuts=7)
    bpy.ops.object.mode_set(mode='OBJECT')
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    for v in bm.verts:
        x, y, z = v.co
        fold = 0.008 * math.sin(x * math.pi / 0.062) * (1.0 - abs(y) / 0.13)
        edge_droop = -0.005 * (abs(x) / 0.200) ** 2
        v.co.z += fold + edge_droop
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    parts = [o]
    o2 = join(parts, "SM_Cloth", bevel_width=0.0006)
    shade_smooth(o2)
    material(o2, "SM_Cloth")
    socket("Socket_Grip", at=(0, 0, 0.060), parent=o2)
    return o2, [o2]


def torch():
    """Hand torch with a lathed ribbed-grip body, a bezelled lens and a
    hanging loop, instead of stacked plain cylinders.
    """
    profile = [
        (0.000, 0.000),
        (0.023, 0.003),
        (0.023, 0.010),
        (0.019, 0.014),
        (0.021, 0.024),
        (0.019, 0.034),
        (0.021, 0.044),
        (0.019, 0.054),
        (0.021, 0.064),
        (0.019, 0.074),
        (0.021, 0.084),
        (0.021, 0.114),
        (0.026, 0.126),
        (0.033, 0.140),
        (0.033, 0.150),
        (0.031, 0.161),
        (0.000, 0.166),
    ]
    body = lathe(profile, seg=16, name="SM_Torch")
    shade_smooth(body)
    parts = [body]
    parts.append(cyl(0.012, 0.010, at=(0, -0.024, 0.100), seg=8,
                     rot=(math.radians(90), 0, 0)))                          # side button
    loop = ring(0.009, 0.0025, at=(0, 0, 0.006), mseg=12, nseg=5)
    parts.append(loop)                                                       # tail hanging loop
    o = join(parts, "SM_Torch", bevel_width=0.0006)
    shade_smooth(o)
    material(o, "SM_Torch")
    socket("Socket_Grip", at=(0, 0, 0.065), parent=o)
    socket("Socket_Light", at=(0, 0, 0.176), facing=(0, 0, 1), parent=o)
    return o, [o]


def health_centre():
    """PROVISIONAL massing, kept crude on purpose (Sec.13 item 4 unresolved),
    but a real hipped roof with overhang, a porch with columns, inset door and
    window openings, and a ridge cap read as a building rather than a block.
    """
    # Overall envelope is pinned to the verified placeholder's bounds
    # (12.6 x 3.65 x 10.3 m, Unity XYZ) -- Verify's size check uses a flat 2cm
    # tolerance regardless of scale, so a 12 m building needs exact numbers,
    # not eyeballed ones. Block depth 8.0 (Blender Y, -4.0..4.0) plus porch
    # depth 2.3 (-6.3..-4.0) makes the 10.3 total; roof ridge height 3.56 with
    # a 0.18 cap makes the 3.65 top.
    RIDGE_BASE, RIDGE_TOP = 3.00, 3.65
    parts = []
    parts.append(box((12.0, 8.0, 3.00), at=(0, 0, 1.50)))                    # main block
    parts.append(box((12.0, 8.0, 0.10), at=(0, 0, 0.05)))                    # plinth course

    # hipped roof: three stacked, inset slabs, reading as a hip from a
    # distance without spending tris on real hip seams. Depth 8.2 (a 0.1 m
    # eave overhang on the back, none on the porch side) plus the 2.2 m porch
    # depth below makes the 10.3 m total.
    parts.append(box((12.6, 8.2, 0.22), at=(0, 0, 3.11)))
    parts.append(box((10.2, 6.6, 0.22), at=(0, 0, 3.34)))
    parts.append(box((7.6, 4.6, 0.18), at=(0, 0, RIDGE_TOP - 0.09)))
    parts.append(box((0.20, 0.20, RIDGE_TOP - RIDGE_BASE),
                     at=(0, 0, (RIDGE_TOP + RIDGE_BASE) / 2)))               # ridge post, hidden

    # porch canopy on -Y with four columns, below the eave line.
    parts.append(box((5.2, 2.2, 0.16), at=(0, -5.10, 2.85)))
    for cx in (-2.1, -0.7, 0.7, 2.1):
        parts.append(cyl(0.09, 2.77, at=(cx, -5.9, 1.385), seg=8))
    parts.append(box((5.6, 2.2, 0.10), at=(0, -5.10, 0.05)))                 # porch slab

    # door and two flanking windows, inset as darker recesses (still one
    # material -- the recess reads from the geometry, not a texture swap).
    parts.append(box((1.60, 0.12, 2.10), at=(0, -4.06, 1.05)))               # door leaf
    for cx in (-3.4, 3.4):
        parts.append(box((1.30, 0.10, 1.20), at=(cx, -4.02, 1.55)))          # window openings
        parts.append(box((1.30, 0.03, 0.05), at=(cx, -3.98, 2.16)))          # lintel

    parts.append(box((2.60, 0.10, 0.66), at=(0, -4.10, 3.05)))               # sign board
    parts.append(box((0.44, 0.11, 0.44), at=(0, -4.16, 3.05)))               # cross emblem block

    o = join(parts, "SM_HealthCentre", bevel_width=0.008)
    shade_flat(o)
    material(o, "SM_HealthCentre")
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
