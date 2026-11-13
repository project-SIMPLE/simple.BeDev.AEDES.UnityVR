#!/usr/bin/env python3
"""
Unity-side companions for the new villager FBX (make_villagers.py):
  - a .meta per generated FBX and per model folder, with a stable GUID, so the asset
    arrives with the same identity for everyone who regenerates it (CLAUDE.md Sec.9: a
    missing .meta breaks every reference to that asset).
  - materialImportMode: 0 (None), matching SK_Character.fbx.meta and every Module 3 prop:
    the FBX carries no usable materials of its own (Blender's Principled BSDF is a preview
    aid only), so Unity should not auto-generate anything from it. The prefab builder
    (Assets/Editor/Module3VillagerPrefabs.cs) assigns the real per-character materials.
  - animationType is deliberately NOT set here. It starts at Unity's default (Generic) on
    first import; Module3VillagerPrefabs.cs configures Humanoid + the bone map via the
    ModelImporter API and reimports, which is the safer way to author a HumanDescription
    than hand-writing its ~150 lines of YAML (see that script's header for why).

Run after make_villagers.py:
    python3 Tools/Module3/make_villager_unity_assets.py

GUIDs are md5("aedes-m3-villager:" + unity asset path) -- a different salt from both the
prop placeholders ("aedes-m3-placeholder:") and prop finals ("aedes-m3-final:"), so none of
the three schemes can collide, and regenerating is idempotent across the team.
"""

import hashlib, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "Assets")
TEAM = os.path.join(ASSETS, "1.TeamWorkspace", "Team Assets")
MODELS = os.path.join(TEAM, "Models")
TEMPLATE_META = os.path.join(MODELS, "SK_Character", "SK_Character.fbx.meta")

VILLAGERS = ["SK_Villager_Elder", "SK_Villager_Child_A", "SK_Villager_Child_B"]


def unity_path(abs_path):
    return "Assets/" + os.path.relpath(abs_path, ASSETS).replace(os.sep, "/")


def guid_for(abs_path):
    h = hashlib.md5(("aedes-m3-villager:" + unity_path(abs_path)).encode())
    return h.hexdigest()


def write(path, text):
    existing = None
    if os.path.exists(path):
        with open(path) as f:
            existing = f.read()
    if existing == text:
        return "unchanged"
    with open(path, "w") as f:
        f.write(text)
    return "written" if existing is None else "updated"


FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def fbx_meta(template, guid):
    """Copies SK_Character.fbx.meta's importer settings (the only skinned-Humanoid FBX in
    the project, so the right serializedVersion/schema to clone) but strips its
    humanDescription and Humanoid animationType: that bone map is SK_Character's own
    ("pelis", "spine1", ...), not this file's, and Module3VillagerPrefabs.cs builds and
    assigns the correct one for each new skeleton via the ModelImporter API instead."""
    out = re.sub(r"^guid: .*$", "guid: " + guid, template, count=1, flags=re.M)
    out = re.sub(r"^(\s*)materialImportMode: .*$", r"\1materialImportMode: 0",
                 out, count=1, flags=re.M)
    out = re.sub(r"^(\s*)animationType: .*$", r"\g<1>animationType: 2",  # Generic; fixed up later
                 out, count=1, flags=re.M)
    out = re.sub(r"    human:\n(?:.*\n)*?(?=    armTwist:)", "    human: []\n    skeleton: []\n",
                 out, count=1)
    return out


def main():
    if not os.path.exists(TEMPLATE_META):
        sys.exit("missing importer template: " + TEMPLATE_META)
    with open(TEMPLATE_META) as f:
        template = f.read()

    results = []
    missing = []
    for name in VILLAGERS:
        folder = os.path.join(MODELS, name)
        fbx = os.path.join(folder, name + ".fbx")
        if not os.path.exists(fbx):
            missing.append(unity_path(fbx))
            continue
        fm = folder + ".meta"
        results.append((unity_path(folder), write(fm, FOLDER_META.format(guid=guid_for(folder)))))
        results.append((unity_path(fbx), write(fbx + ".meta", fbx_meta(template, guid_for(fbx)))))

    for p, s in results:
        print(f"  {s:<9} {p}")
    print(f"\n{len(results)} files")
    if missing:
        print("\nMissing FBX (run make_villagers.py first):")
        for m in missing:
            print("  ", m)
        sys.exit(1)


if __name__ == "__main__":
    main()
