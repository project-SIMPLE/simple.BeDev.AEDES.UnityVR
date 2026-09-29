#!/usr/bin/env blender --python
"""
New villager bodies for Module 3, addressing Documentation/Module3-Animation-Brief.md §7
("Age variety - a question, not a task"): the neighbourhood has three adult body meshes
sharing one skeleton (fodo/nasa/Roger, 592-712 tris) but no child or elder body, so
M3NeighbourhoodBuilder fakes both by uniformly scaling an adult -- which §7 warns reads as
"a small adult, not a child".

This delivers Option 2 from that section: new meshes on their own Humanoid skeletons, so
whatever clips retarget to SK_Character also retarget here with no extra animation work
(the idle already in SK_Character.fbx is a Humanoid clip, so these stand in that idle
today). Two new rigs:

  - a child skeleton, carrying two mesh variants (SK_Villager_Child_A, SK_Villager_Child_B)
    the way fodo/nasa/Roger share one adult skeleton -- the second is nearly free.
  - an elder skeleton (adult-scale but shorter, SK_Villager_Elder).

STYLE MATCH. SK_Character is a super-deformed figure: head about a third of total height,
short legs (~35%), A-pose arms, hands as small hooks. These follow the same language
(measured from its bones, see the proportion tables) so a villager of each age can share a
room without one of them looking like a different game. Children push it further (head ~40%).

FACING. SK_Character faces Blender -Y (its feet point -Y), which is Unity +Z after the
exporter's own Z-up -> Y-up conversion; VillagerView and the builder aim villagers with
LookRotation, i.e. they assume +Z is forward. Everything here faces -Y for the same reason.

LAO VILLAGE CONTEXT is carried by dress and setting, never by face or body shape:
  - bare feet (people go barefoot inside Lao homes, and every scene here is indoors),
  - a sinh-style ankle-length wrap skirt with a contrasting hem band, plus a shoulder sash
    (pha biang) for the elder; a collared tee/shorts and a tunic-and-skirt for the children,
  - hair silhouettes (a low bun, pigtails, a crop) and hair colour.
The head is a plain rounded shape for everyone. The only facial feature is the same two plain
vertical eye bars the existing SK_Character has, identical on every villager -- there is no
facial rig in this project (Animation Brief §9), and no nose, mouth or shaped eyes. That is
exactly why detail belongs in clothing and colour, never in invented facial or "racial" features.

Run:
    blender -b --factory-startup --python Tools/Module3/make_villagers.py

Unlike make_final_models.py (static props with parented Socket_* empties, hence its custom
no-conversion axis handling), this uses Blender's ORDINARY default FBX export axes for a
skinned character (forward -Z, up Y), the standard Blender-to-Unity humanoid pipeline.
"""

import bpy, bmesh, math, os, sys, json
from mathutils import Vector

BONE_NAMES = [
    "Hips", "Spine", "Head",
    "Shoulder.L", "UpperArm.L", "LowerArm.L", "Hand.L",
    "Shoulder.R", "UpperArm.R", "LowerArm.R", "Hand.R",
    "UpperLeg.L", "LowerLeg.L", "Foot.L",
    "UpperLeg.R", "LowerLeg.R", "Foot.R",
]
PARENT = {
    "Spine": "Hips", "Head": "Spine",
    "Shoulder.L": "Spine", "UpperArm.L": "Shoulder.L", "LowerArm.L": "UpperArm.L", "Hand.L": "LowerArm.L",
    "Shoulder.R": "Spine", "UpperArm.R": "Shoulder.R", "LowerArm.R": "UpperArm.R", "Hand.R": "LowerArm.R",
    "UpperLeg.L": "Hips", "LowerLeg.L": "UpperLeg.L", "Foot.L": "LowerLeg.L",
    "UpperLeg.R": "Hips", "LowerLeg.R": "UpperLeg.R", "Foot.R": "LowerLeg.R",
}

TRI_BUDGET = {
    "SK_Villager_Elder":   1800,
    "SK_Villager_Child_A": 1800,
    "SK_Villager_Child_B": 1800,
}

# Flat colours (URP/Lit, one region each). One plain warm-brown skin tone
# shared by every villager: nothing here codes a specific ethnicity beyond "this village",
# and there is no facial geometry that could caricature one. "Unwell" is the same tone made
# paler and a little greyer -- tired, not diseased (Animation Brief §2).
SKIN            = (0.78, 0.58, 0.43)
SKIN_UNWELL     = (0.84, 0.70, 0.60)
EYE             = (0.06, 0.05, 0.05)   # the same two plain dark bars on everyone
EYE_KEY         = "M_Villager_Eye"     # one material shared by all three characters
HAIR            = (0.09, 0.07, 0.06)
HAIR_GREY       = (0.70, 0.68, 0.66)
CLOTH_ELDER     = (0.24, 0.29, 0.46)   # indigo sinh + blouse
TRIM_ELDER      = (0.58, 0.16, 0.18)   # deep red hem band / sash / waistband
CLOTH_CHILD_A   = (0.28, 0.55, 0.55)   # teal tee + shorts
TRIM_CHILD_A    = (0.92, 0.87, 0.72)   # cream collar / hem
CLOTH_CHILD_B   = (0.80, 0.60, 0.22)   # ochre tunic + skirt
TRIM_CHILD_B    = (0.52, 0.20, 0.16)   # brick red collar / hem / hair ties

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0

def _added():
    return bpy.context.active_object

def lerp(a, b, t):
    return Vector(a) * (1 - t) + Vector(b) * t

def cyl(r, h, at, rot=(0, 0, 0), seg=12, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=seg, location=at, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(radius1=r, radius2=r2, depth=h, vertices=seg, location=at, rotation=rot)
    return _added()

def box(size, at):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    o = _added()
    o.scale = size
    return o

def ball(r, at, seg=14, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=seg, ring_count=max(5, seg // 2), location=at)
    o = _added()
    if scale != (1, 1, 1):
        o.scale = scale
    return o

def ring(major, minor, at, rot=(0, 0, 0), mseg=16, nseg=6):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=mseg,
                                     minor_segments=nseg, location=at, rotation=rot)
    return _added()

def hair_cap(r, at, seg=16, back_rim=-0.72, front_rim=0.30, front_y=0.2):
    """A hair shell over the head: the top of a sphere, kept down to `back_rim` (a fraction of
    r, negative = below the equator) at the sides and back, but only down to `front_rim` across
    the front (-Y, where the character faces), which is what gives a forehead hairline instead of
    a visor. Vertices are kept or dropped by sphere latitude, so the thresholds sit between rings
    (a 16x8 UV sphere has rings at z/r = +-0.92, +-0.71, +-0.38, 0). An open shell, not a solid
    dome with a lid: no lower hemisphere to poke through the scalp, and no cap face sunk inside
    the head to cost triangles."""
    o = ball(r, at=(0, 0, 0), seg=seg)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    doomed = [v for v in bm.verts
              if v.co.z < back_rim * r or (v.co.y < -front_y * r and v.co.z < front_rim * r)]
    bmesh.ops.delete(bm, geom=doomed, context='VERTS')
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    o.location = at
    return o

def seg_between(p0, p1, r, seg=12, r2=None):
    """A cylinder (optionally tapered) spanning two joint points, oriented automatically."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    mid = (p0 + p1) / 2
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler()
    return cyl(r, d.length, at=mid, rot=rot, seg=seg, r2=r2)

def band_on(a, b, t0, t1, r, seg=10):
    """A short wide cylinder wrapped around the limb between fractions t0..t1 of a->b
    (a hem band, a cuff): trim colour, bound to whichever bone the limb belongs to."""
    return seg_between(lerp(a, b, t0), lerp(a, b, t1), r, seg=seg)

def rigid_group(obj, bone_name):
    """Every vertex of `obj` -> a vertex group named after `bone_name`, full weight. Blender's
    join merges same-named groups, so each source object's binding survives as that part's
    rigid skin. Predictable for a blocky low-poly character (no heat-map artefacts), and these
    are idle-class poses (Animation Brief §4) with small joint rotations, so a hard bind reads
    fine."""
    vg = obj.vertex_groups.new(name=bone_name)
    vg.add(range(len(obj.data.vertices)), 1.0, 'REPLACE')

def join(parts, name):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.name = name
    o.data.name = name
    return o

def shade_smooth(o):
    for p in o.data.polygons:
        p.use_smooth = True

def material(o, key, color):
    m = bpy.data.materials.get(key) or bpy.data.materials.new(key)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.7
    m.diffuse_color = (*color, 1.0)
    o.data.materials.append(m)
    for p in o.data.polygons:
        p.material_index = len(o.data.materials) - 1

# ---------------------------------------------------------------------------
# Skeleton
# ---------------------------------------------------------------------------

def build_skeleton(bones, name):
    """`bones` maps bone name -> (head_xyz, tail_xyz), A-pose, feet on z=0."""
    arm_data = bpy.data.armatures.new(name)
    arm_obj = bpy.data.objects.new(name, arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones
    for bone_name in BONE_NAMES:
        b = eb.new(bone_name)
        b.head, b.tail = bones[bone_name]
        b.align_roll((0, -1, 0) if ("Leg" in bone_name or "Foot" in bone_name) else (0, 0, 1))
    for bone_name, parent_name in PARENT.items():
        eb[bone_name].parent = eb[parent_name]
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm_obj

def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

# ---------------------------------------------------------------------------
# Joint layout, from proportions
# ---------------------------------------------------------------------------

def layout(p):
    """All joint points for a proportions dict. Knees bend slightly forward (-Y) and elbows
    slightly back so Unity's Humanoid mapper gets an unambiguous hinge direction -- a
    perfectly straight chain leaves the bend axis to chance, and SK_Character does the same
    (its knees sit ~3 cm forward of the hip-ankle line)."""
    hx = p["hip_x"]
    arm_z = p["chest_z"] - p["arm_drop"]
    a = math.radians(p["arm_angle"])
    b = math.radians(p["arm_angle"] + 6)
    J = {}
    for side, sx in (("L", 1), ("R", -1)):
        J[f"hip_{side}"] = Vector((sx * hx, 0, p["hip_z"]))
        J[f"knee_{side}"] = Vector((sx * hx, -p["knee_fwd"], p["knee_z"]))
        J[f"ankle_{side}"] = Vector((sx * hx, 0, p["ankle_z"]))
        J[f"toe_{side}"] = Vector((sx * hx, -p["foot_len"], 0.0))
        s_in = Vector((sx * p["sh_in_x"], 0, arm_z))
        s_out = Vector((sx * p["sh_out_x"], 0, arm_z - 0.01))
        elbow = s_out + Vector((sx * math.cos(a), 0, -math.sin(a))) * p["Lu"] + Vector((0, p["elbow_back"], 0))
        wrist = elbow + Vector((sx * math.cos(b), 0, -math.sin(b))) * p["Lf"] - Vector((0, p["elbow_back"], 0))
        tip = wrist + Vector((sx * math.cos(b), 0, -math.sin(b))) * p["Lh"]
        J[f"sh_in_{side}"], J[f"sh_out_{side}"] = s_in, s_out
        J[f"elbow_{side}"], J[f"wrist_{side}"], J[f"hand_{side}"] = elbow, wrist, tip
    bones = {
        "Hips":  (Vector((0, 0, p["hip_z"])), Vector((0, 0, p["hip_z"] + 0.05))),
        "Spine": (Vector((0, 0, p["hip_z"])), Vector((0, 0, p["chest_z"]))),
        "Head":  (Vector((0, 0, p["chest_z"])), Vector((0, 0, p["head_top_z"]))),
    }
    for side in ("L", "R"):
        bones[f"Shoulder.{side}"] = (J[f"sh_in_{side}"], J[f"sh_out_{side}"])
        bones[f"UpperArm.{side}"] = (J[f"sh_out_{side}"], J[f"elbow_{side}"])
        bones[f"LowerArm.{side}"] = (J[f"elbow_{side}"], J[f"wrist_{side}"])
        bones[f"Hand.{side}"] = (J[f"wrist_{side}"], J[f"hand_{side}"])
        bones[f"UpperLeg.{side}"] = (J[f"hip_{side}"], J[f"knee_{side}"])
        bones[f"LowerLeg.{side}"] = (J[f"knee_{side}"], J[f"ankle_{side}"])
        bones[f"Foot.{side}"] = (J[f"ankle_{side}"], J[f"toe_{side}"])
    return J, bones

# ---------------------------------------------------------------------------
# Body builder -- shared by every variant
# ---------------------------------------------------------------------------

def build_body(name, p, o):
    """
    `o` (outfit) keys:
      skin_key, cloth_key/cloth_color, hair_key/hair_color, trim_key/trim_color
      hair_style  'crop' | 'bun' | 'pigtails'
      legs        'skirt_long' | 'shorts' | 'skirt_short'
      sleeves     'long' | 'short'
      sash        bool       diagonal shoulder cloth (pha biang)
      waistband   bool
      collar      bool
    Parts are appended in a fixed order -- Skin, Hair, Cloth, Trim, Eye -- because Blender's join
    gives the joined mesh its material slots in first-appearance order, and Skin MUST be
    slot 0: VillagerView.Refresh() swaps the well/unwell skin through the singular
    `skinRenderer.sharedMaterial`, which always addresses slot 0.
    """
    J, bones = layout(p)
    arm_obj = build_skeleton(bones, name + "_Armature")
    parts = []

    def add(obj, bone, key, color):
        material(obj, key, color)
        rigid_group(obj, bone)
        parts.append(obj)

    skin_k, cloth_k, hair_k, trim_k = o["skin_key"], o["cloth_key"], o["hair_key"], o["trim_key"]
    cloth_c, hair_c, trim_c = o["cloth_color"], o["hair_color"], o["trim_color"]
    hr = p["head_r"]
    hc = (p["chest_z"] + p["head_top_z"]) / 2

    # ---- Skin first: head and ears ----
    add(ball(hr, (0, 0, hc), seg=14), "Head", skin_k, SKIN)
    for sx in (1, -1):
        add(ball(hr * 0.15, (sx * hr * 0.99, 0, hc - hr * 0.04), seg=6, scale=(0.5, 0.9, 1.0)),
            "Head", skin_k, SKIN)

    # ---- Hair. A true hemispherical cap (no nested sphere to poke through the head).
    # Everything hangs BEHIND the head, i.e. towards +Y (the character faces -Y). ----
    if o["hair_style"] == "bun":
        add(hair_cap(hr * 1.06, (0, 0, hc), back_rim=-0.60), "Head", hair_k, hair_c)
        add(ball(hr * 0.30, (0, hr * 1.02, hc + hr * 0.22), seg=10), "Head", hair_k, hair_c)
    elif o["hair_style"] == "pigtails":
        add(hair_cap(hr * 1.06, (0, 0, hc), back_rim=-0.45), "Head", hair_k, hair_c)
        for sx in (1, -1):
            add(seg_between((sx * hr * 0.90, hr * 0.35, hc + hr * 0.04),
                            (sx * hr * 1.22, hr * 0.55, hc - hr * 0.70), r=hr * 0.15, seg=10),
                "Head", hair_k, hair_c)
    else:
        add(hair_cap(hr * 1.05, (0, 0, hc), back_rim=-0.45), "Head", hair_k, hair_c)

    # ---- Cloth: torso, pelvis, sleeves, legs ----
    trb, trt = p["torso_rb"], p["torso_rt"]
    add(seg_between((0, 0, p["hip_z"] - 0.02), (0, 0, p["chest_z"]), r=trb, r2=trt, seg=12),
        "Spine", cloth_k, cloth_c)
    add(cyl(max(trb * 1.03, p["hip_x"] + p["leg_r"] * 1.12), 0.10, (0, 0, p["hip_z"] + 0.01), seg=12),
        "Hips", cloth_k, cloth_c)

    for side in ("L", "R"):
        add(ball(p["arm_r"] * 1.3, J[f"sh_out_{side}"], seg=10), f"Shoulder.{side}", cloth_k, cloth_c)
        add(seg_between(J[f"sh_out_{side}"], J[f"elbow_{side}"], r=p["arm_r"], seg=10),
            f"UpperArm.{side}", cloth_k, cloth_c)
        if o["sleeves"] == "long":
            add(seg_between(J[f"elbow_{side}"], J[f"wrist_{side}"], r=p["arm_r"] * 0.92,
                            r2=p["arm_r"] * 0.82, seg=10), f"LowerArm.{side}", cloth_k, cloth_c)

    lr = p["leg_r"]
    for side in ("L", "R"):
        hip, knee, ankle = J[f"hip_{side}"], J[f"knee_{side}"], J[f"ankle_{side}"]
        if o["legs"] == "skirt_long":            # tubes sit inside the skirt cone below
            add(seg_between(hip, knee, r=lr * 1.10, r2=lr * 1.05, seg=10), f"UpperLeg.{side}", cloth_k, cloth_c)
            add(seg_between(knee, ankle, r=lr * 1.02, r2=lr * 0.95, seg=10), f"LowerLeg.{side}", cloth_k, cloth_c)
        elif o["legs"] == "skirt_short":
            add(seg_between(hip, knee, r=lr * 1.10, r2=lr * 1.05, seg=10), f"UpperLeg.{side}", cloth_k, cloth_c)
        else:                                    # shorts
            add(seg_between(hip, knee, r=lr * 1.12, r2=lr * 1.08, seg=10), f"UpperLeg.{side}", cloth_k, cloth_c)

    # A wrapped skirt (sinh) is ONE tube round both legs; two separate leg tubes read as wide
    # trousers. The cone is bound to the hips: it hangs from the waist, and while the whole
    # rig is tipped flat to lie on a bed (VillagerView) it goes with the body. A future seated
    # clip would leave it standing round the pelvis with the thighs coming out of the front --
    # an A-line drape, acceptable, and animation-safe (nothing pulls it through the legs).
    skirt_bottom = None
    if o["legs"] in ("skirt_long", "skirt_short"):
        z_top = p["hip_z"] + 0.03
        z_bot = (p["ankle_z"] + 0.03) if o["legs"] == "skirt_long" else (p["knee_z"] - 0.03)
        r_top = p["hip_x"] + lr * 1.10 + 0.006
        r_bot = p["skirt_r"]
        add(seg_between((0, 0, z_top), (0, 0, z_bot), r=r_top, r2=r_bot, seg=14), "Hips", cloth_k, cloth_c)
        skirt_bottom = (z_top, z_bot, r_top, r_bot)

    # ---- Trim: collar, waistband, sash, hems, cuffs (a contrast colour, one material) ----
    if o["collar"]:
        add(ring(trt * 0.93, 0.024 * (p["head_r"] / 0.26), (0, 0, p["chest_z"] - 0.035), mseg=14, nseg=5),
            "Spine", trim_k, trim_c)
    if o["waistband"]:
        add(ring(trb * 1.02, 0.03, (0, 0, p["hip_z"] + 0.055), mseg=14, nseg=5), "Hips", trim_k, trim_c)
    if o["sash"]:
        # pha biang: a cloth worn diagonally from one shoulder to the opposite hip. A flattened
        # ring tilted about Y reads as that band from the front without needing a skinned strip.
        rmid = (trb + trt) / 2 * 1.06
        sash = ring(rmid, 0.028, (0, 0, (p["hip_z"] + p["chest_z"]) / 2 + 0.02),
                    rot=(0, math.radians(38), 0), mseg=16, nseg=5)
        sash.scale = (1.0, 1.0, 1.15)
        add(sash, "Spine", trim_k, trim_c)
    if skirt_bottom:
        z_top, z_bot, r_top, r_bot = skirt_bottom
        def skirt_r(t):                         # cone radius, t=0 at the waist, 1 at the hem
            return r_top * (1 - t) + r_bot * t
        add(seg_between((0, 0, z_top + (z_bot - z_top) * 0.84), (0, 0, z_bot),
                        r=skirt_r(0.84) * 1.03, r2=skirt_r(1.0) * 1.03, seg=14),
            "Hips", trim_k, trim_c)
    for side in ("L", "R"):
        hip, knee = J[f"hip_{side}"], J[f"knee_{side}"]
        if o["legs"] == "shorts":
            add(band_on(hip, knee, 0.86, 1.0, r=lr * 1.14), f"UpperLeg.{side}", trim_k, trim_c)
        if o["sleeves"] == "long":
            add(band_on(J[f"elbow_{side}"], J[f"wrist_{side}"], 0.80, 0.98, r=p["arm_r"] * 0.86 * 1.16),
                f"LowerArm.{side}", trim_k, trim_c)
    if o["hair_style"] == "pigtails":
        for sx in (1, -1):
            add(ball(hr * 0.13, (sx * hr * 0.92, hr * 0.36, hc + hr * 0.06), seg=8), "Head", trim_k, trim_c)

    # ---- Eyes: the same two plain vertical bars SK_Character has, identical on every villager.
    # Nothing else on the face -- no nose, no mouth, no shaping of the eyes. Added after Trim so
    # the eye material is the last slot (Skin, Hair, Cloth, Trim, Eye). ----
    for sx in (1, -1):
        add(box((0.09 * hr, 0.05 * hr, 0.26 * hr), (sx * 0.24 * hr, -0.955 * hr, hc + 0.10 * hr)),
            "Head", EYE_KEY, EYE)

    # ---- Bare skin below the clothing: forearms (short sleeves), hands, feet ----
    for side in ("L", "R"):
        if o["sleeves"] == "short":
            add(seg_between(J[f"elbow_{side}"], J[f"wrist_{side}"], r=p["arm_r"] * 0.85,
                            r2=p["arm_r"] * 0.72, seg=10), f"LowerArm.{side}", skin_k, SKIN)
        add(seg_between(J[f"wrist_{side}"], J[f"hand_{side}"], r=p["arm_r"] * 0.78,
                        r2=p["arm_r"] * 0.62, seg=10), f"Hand.{side}", skin_k, SKIN)
        if o["legs"] != "skirt_long":
            add(seg_between(J[f"knee_{side}"], J[f"ankle_{side}"], r=lr * 0.90, r2=lr * 0.66, seg=10),
                f"LowerLeg.{side}", skin_k, SKIN)
        ankle, toe = J[f"ankle_{side}"], J[f"toe_{side}"]
        heel = Vector((ankle.x, ankle.y + 0.03, ankle.z * 0.5))
        add(seg_between(heel, Vector((toe.x, toe.y * 0.85, 0.028)), r=lr * 0.70, r2=lr * 0.55, seg=10),
            f"Foot.{side}", skin_k, SKIN)
        add(ball(lr * 0.52, Vector((toe.x, toe.y * 0.92, 0.030)), seg=6, scale=(1.0, 0.85, 0.7)),
            f"Foot.{side}", skin_k, SKIN)

    body = join(parts, name)
    shade_smooth(body)
    body.parent = arm_obj
    mod = body.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm_obj
    return arm_obj, body

# ---------------------------------------------------------------------------
# Proportions -- measured from SK_Character's bones (1.69 m adult: head bone 1.13 -> 1.57,
# hips 0.58, knee 0.34, ankle 0.10, shoulders +-0.18, upper arm 0.25 / forearm 0.19 / hand
# 0.10 at ~20-25 deg below horizontal), scaled to the elder's height. Children push the
# head ratio further.
# ---------------------------------------------------------------------------
ELDER_PROPORTIONS = dict(
    ankle_z=0.09, knee_z=0.31, hip_z=0.56, chest_z=0.99, head_top_z=1.58,
    hip_x=0.115, sh_in_x=0.05, sh_out_x=0.17, arm_drop=0.05, arm_angle=24,
    Lu=0.225, Lf=0.185, Lh=0.09,
    head_r=0.262, arm_r=0.050, leg_r=0.088, foot_len=0.17,
    torso_rb=0.19, torso_rt=0.20, knee_fwd=0.02, elbow_back=0.012, skirt_r=0.255,
)
CHILD_PROPORTIONS = dict(
    ankle_z=0.06, knee_z=0.20, hip_z=0.36, chest_z=0.62, head_top_z=1.10,
    hip_x=0.082, sh_in_x=0.04, sh_out_x=0.115, arm_drop=0.035, arm_angle=24,
    Lu=0.15, Lf=0.12, Lh=0.065,
    head_r=0.225, arm_r=0.036, leg_r=0.062, foot_len=0.12,
    torso_rb=0.14, torso_rt=0.145, knee_fwd=0.014, elbow_back=0.008, skirt_r=0.205,
)

VARIANTS = [
    ("SK_Villager_Elder", ELDER_PROPORTIONS, dict(
        skin_key="M_Villager_Elder_Skin", cloth_key="M_Villager_Elder_Cloth", cloth_color=CLOTH_ELDER,
        hair_key="M_Villager_Elder_Hair", hair_color=HAIR_GREY,
        trim_key="M_Villager_Elder_Trim", trim_color=TRIM_ELDER,
        hair_style="bun", legs="skirt_long", sleeves="long", sash=True, waistband=True, collar=False,
    )),
    ("SK_Villager_Child_A", CHILD_PROPORTIONS, dict(
        skin_key="M_Villager_ChildA_Skin", cloth_key="M_Villager_ChildA_Cloth", cloth_color=CLOTH_CHILD_A,
        hair_key="M_Villager_ChildA_Hair", hair_color=HAIR,
        trim_key="M_Villager_ChildA_Trim", trim_color=TRIM_CHILD_A,
        hair_style="crop", legs="shorts", sleeves="short", sash=False, waistband=False, collar=True,
    )),
    ("SK_Villager_Child_B", CHILD_PROPORTIONS, dict(
        skin_key="M_Villager_ChildB_Skin", cloth_key="M_Villager_ChildB_Cloth", cloth_color=CLOTH_CHILD_B,
        hair_key="M_Villager_ChildB_Hair", hair_color=HAIR,
        trim_key="M_Villager_ChildB_Trim", trim_color=TRIM_CHILD_B,
        hair_style="pigtails", legs="skirt_short", sleeves="short", sash=False, waistband=True, collar=True,
    )),
]


def export(mesh_obj, armature_obj, name, out_dir):
    d = os.path.join(out_dir, name)
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, name + ".fbx")
    bpy.ops.object.select_all(action='DESELECT')
    armature_obj.select_set(True)
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        mesh_smooth_type='FACE',
        use_armature_deform_only=True,
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
        out_dir = os.path.join(repo, "Assets", "1.TeamWorkspace", "Team Assets", "Models")

    report, failures = [], []
    for name, proportions, outfit in VARIANTS:
        reset()
        arm_obj, body = build_body(name, proportions, outfit)
        t = tris(body)
        budget = TRI_BUDGET[name]
        if t > budget:
            failures.append(f"{name}: {t} tris over budget {budget}")
        zs = [v.co.z for v in body.data.vertices]
        ys_low = [v.co.y for v in body.data.vertices if v.co.z < 0.05]
        fwd = min(ys_low) if ys_low else 0.0     # toes: must be NEGATIVE (faces -Y)
        back = max(ys_low) if ys_low else 0.0
        if not (fwd < -0.05 and abs(fwd) > abs(back)):
            failures.append(f"{name}: feet do not point -Y (toe y {fwd:.3f}, heel y {back:.3f})")
        path = export(body, arm_obj, name, out_dir)
        report.append({
            "asset": name, "tris": t, "budget": budget,
            "height_m": round(max(zs) - min(zs), 3),
            "materials": [m.name for m in body.data.materials],
            "bones": len(arm_obj.data.bones),
            "toe_y": round(fwd, 3), "heel_y": round(back, 3),
        })

    print("===REPORT===")
    print(json.dumps(report, indent=1))
    if failures:
        print("===FAILURES===")
        for f in failures:
            print(" ", f)
        sys.exit(1)
    print("===OK=== %d villagers" % len(report))


if __name__ == "__main__":
    main()
