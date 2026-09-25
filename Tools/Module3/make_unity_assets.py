#!/usr/bin/env python3
"""
Unity-side companions for the Module 3 greybox placeholders:
  - a .meta per generated FBX and per model folder, so the assets arrive with
    stable GUIDs instead of whatever the first person to open Unity gets
    (roadmap Sec.4: "commit with .meta files"; CLAUDE.md Sec.9: a missing .meta
    breaks every reference to that asset for everyone else)
  - M_Module3Placeholder.mat, one flat grey URP material on the project's own
    SHD_SimpleLit_Static graph

Run after make_placeholders.py:
    python3 Tools/Module3/make_unity_assets.py

GUIDs are md5("aedes-m3-placeholder:" + unity asset path), so re-running is
idempotent and two people generating these independently get the same GUIDs.

The FBX importer settings are cloned from an existing repo model
(SM_Bed.fbx.meta) so the placeholders import exactly like the art around them,
with one deliberate change: materialImportMode is set to 0 (None). The
placeholders carry no embedded materials; the prefab builder assigns
M_Module3Placeholder explicitly, so the material is a real, editable asset on
the project's own shader rather than an FBX-embedded Standard-shader material.
"""

import hashlib, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "Assets")
TEAM = os.path.join(ASSETS, "1.TeamWorkspace", "Team Assets")
MODELS = os.path.join(TEAM, "Models")
MATERIALS = os.path.join(TEAM, "Materials")
TEMPLATE_META = os.path.join(MODELS, "SM_Bed", "SM_Bed.fbx.meta")

# Existing project assets the material is built from.
SHADER_GUID = "850a53dfcf2e85a4d8365db9ad81bf96"   # SHD_SimpleLit_Static
SHADER_FILEID = "-6465566751694194690"
URP_ASSETVERSION_GUID = "d0353a89b1f911e48b9e16bdc9f2e058"

PLACEHOLDERS = [
    "SM_ElectricFan",
    "SM_WindowScreen",
    "SM_WindowScreenTorn",
    "SM_MosquitoNet_RolledUp",
    "SM_RepellentBottle",
    "SM_DrinkingVessel",
    "SM_Cloth",
    "SM_Torch",
    "SM_HealthCentre",
]

MATERIAL_NAME = "M_Module3Placeholder"
GREY = (0.62, 0.62, 0.60)


def unity_path(abs_path):
    return "Assets/" + os.path.relpath(abs_path, ASSETS).replace(os.sep, "/")


def guid_for(abs_path):
    h = hashlib.md5(("aedes-m3-placeholder:" + unity_path(abs_path)).encode())
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
    out = re.sub(r"^guid: .*$", "guid: " + guid, template, count=1, flags=re.M)
    # No embedded materials -- the prefab assigns M_Module3Placeholder.
    out = re.sub(r"^(\s*)materialImportMode: .*$", r"\1materialImportMode: 0",
                 out, count=1, flags=re.M)
    return out


MATERIAL_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: {shader_fileid}, guid: {shader_guid}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _Texture_01:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AddPrecomputedVelocity: 0
    - _Additionnal_Color: 0
    - _AlphaClip: 0
    - _AlphaToMask: 0
    - _Blend: 0
    - _BlendModePreserveSpecular: 1
    - _BumpScale: 1
    - _ClearCoatMask: 0
    - _ClearCoatSmoothness: 0
    - _Cull: 2
    - _Cutoff: 0.5
    - _Dissolve: 0
    - _DstBlend: 0
    - _DstBlendAlpha: 0
    - _EnvironmentReflections: 1
    - _Fade: 0
    - _GlossMapScale: 0
    - _Glossiness: 0
    - _GlossyReflections: 0
    - _Highlight: 0
    - _Metallic: 0
    - _OcclusionStrength: 1
    - _Parallax: 0.005
    - _QueueControl: 0
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _ShadowLimit: -0.14
    - _Smoothness: 0.5
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _SrcBlendAlpha: 1
    - _Surface: 0
    - _Texture: 0
    - _WorkflowMode: 1
    - _XRMotionVectorsPass: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {{r: {r}, g: {g}, b: {b}, a: 1}}
    - _Color: {{r: {r}, g: {g}, b: {b}, a: 1}}
    - _Color1: {{r: 0, g: 0, b: 0, a: 0}}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}
    - _Highlight_Color: {{r: 1, g: 1, b: 1, a: 1}}
    - _Multiply_Color: {{r: 1, g: 1, b: 1, a: 1}}
    - _Shadow_Color: {{r: 0.75, g: 0.75, b: 0.78, a: 0}}
    - _SpecColor: {{r: 0.2, g: 0.2, b: 0.2, a: 1}}
    - _UV_Tiling: {{r: 1, g: 1, b: 0, a: 0}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
--- !u!114 &6168096758479283809
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {urp_version_guid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
"""

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def main():
    if not os.path.exists(TEMPLATE_META):
        sys.exit("missing importer template: " + TEMPLATE_META)
    with open(TEMPLATE_META) as f:
        template = f.read()

    results = []
    missing = []

    for name in PLACEHOLDERS:
        folder = os.path.join(MODELS, name)
        fbx = os.path.join(folder, name + ".fbx")
        if not os.path.exists(fbx):
            missing.append(unity_path(fbx))
            continue
        # folder .meta
        fm = folder + ".meta"
        results.append((unity_path(folder),
                        write(fm, FOLDER_META.format(guid=guid_for(folder)))))
        # fbx .meta
        results.append((unity_path(fbx),
                        write(fbx + ".meta", fbx_meta(template, guid_for(fbx)))))

    # placeholder material
    os.makedirs(MATERIALS, exist_ok=True)
    mat = os.path.join(MATERIALS, MATERIAL_NAME + ".mat")
    r, g, b = GREY
    results.append((unity_path(mat), write(mat, MATERIAL_TEMPLATE.format(
        name=MATERIAL_NAME, shader_fileid=SHADER_FILEID,
        shader_guid=SHADER_GUID, urp_version_guid=URP_ASSETVERSION_GUID,
        r=r, g=g, b=b))))
    results.append((unity_path(mat) + ".meta",
                    write(mat + ".meta",
                          ASSET_META.format(guid=guid_for(mat)))))

    w = max(len(p) for p, _ in results)
    for p, s in results:
        print(f"  {s:<9} {p}")
    print(f"\n{len(results)} files; material GUID {guid_for(mat)}")
    if missing:
        print("\nMissing FBX (run make_placeholders.py first):")
        for m in missing:
            print("  ", m)
        sys.exit(1)


if __name__ == "__main__":
    main()
