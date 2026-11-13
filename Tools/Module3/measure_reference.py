#!/usr/bin/env blender --python
"""
Measure existing repo art, so placeholder dimensions are derived from the real
assets rather than invented.

    blender -b --factory-startup --python Tools/Module3/measure_reference.py -- <a.fbx> [b.fbx ...]

Prints, per file, each node's world-space size and bounds, its triangle count,
and the file's overall bounding box. Sizes come back in Blender's Z-up metres:
"height" is the third component. These numbers are the source of the MEASURED
constants at the top of make_placeholders.py.
"""

import bpy, sys, os, json
from mathutils import Vector


def bounds(objs):
    pts = []
    for o in objs:
        if o.type == 'MESH':
            pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    if not pts:
        return None, None
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


def main():
    if "--" not in sys.argv:
        sys.exit("pass FBX paths after --")
    paths = sys.argv[sys.argv.index("--") + 1:]

    out = {}
    for p in paths:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        try:
            # Blender 5.x ships the newer importer; older builds only have the
            # Python one.
            if hasattr(bpy.ops.wm, "fbx_import"):
                bpy.ops.wm.fbx_import(filepath=p)
            else:
                bpy.ops.import_scene.fbx(filepath=p)
        except Exception as e:
            out[os.path.basename(p)] = {"IMPORT_FAILED": str(e)}
            continue

        objs = list(bpy.data.objects)
        info = {"nodes": []}
        for o in objs:
            rec = {"name": o.name, "type": o.type,
                   "parent": o.parent.name if o.parent else None,
                   "local_loc": [round(v, 4) for v in o.location],
                   "scale": [round(v, 4) for v in o.scale]}
            if o.type == 'MESH':
                mn, mx = bounds([o])
                rec["size"] = [round(v, 4) for v in (mx - mn)]
                rec["min"] = [round(v, 4) for v in mn]
                rec["max"] = [round(v, 4) for v in mx]
                rec["tris"] = sum(len(pg.vertices) - 2 for pg in o.data.polygons)
                rec["materials"] = [m.name for m in o.data.materials if m]
            info["nodes"].append(rec)

        mn, mx = bounds(objs)
        if mn:
            info["total_size"] = [round(v, 4) for v in (mx - mn)]
            info["total_min"] = [round(v, 4) for v in mn]
            info["total_max"] = [round(v, 4) for v in mx]
        out[os.path.basename(p)] = info

    print("===MEASURED===")
    print(json.dumps(out, indent=1))


if __name__ == "__main__":
    main()
