#!/usr/bin/env python3
"""
Per-character materials for the new villager bodies (make_villagers.py): Skin, a paler
Skin_Unwell variant, Hair, Cloth and Trim (a contrast colour for hem bands, sashes and collars),
one flat colour each on URP/Lit -- the same convention as every other Module 3
asset (Asset Brief Sec.2: one material per region, no PBR, toon/baked-lighting style).

The Skin/Skin_Unwell pair exists to fill VillagerView's `wellSkin`/`unwellSkin` fields
(Module3VillagerPrefabs.cs wires them on the built prefab): "unwell" is a paler version of
the same skin tone, never a colour or hue shift, per the Animation Brief Sec.2 framing rule
-- tired, not diseased.

Run after make_villagers.py:
    python3 Tools/Module3/make_villager_materials.py

GUIDs are md5("aedes-m3-villager:" + unity asset path) -- the same salt as
make_villager_unity_assets.py, since these are part of the same asset family.
"""

import hashlib, os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "Assets")
TEAM = os.path.join(ASSETS, "1.TeamWorkspace", "Team Assets")
MATERIALS = os.path.join(TEAM, "Materials")

# Universal Render Pipeline/Lit, like the rest of Team Assets and like M_Module3Placeholder.mat now is.
# NOT the project's SHD_SimpleLit_Static graph: that toon graph takes its colour from a gradient texture
# (_Texture_01) and ignores _BaseColor, so a flat colour on it renders solid black in the game -- which is
# exactly what the first version of these materials did until the scene was played and shot.
SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"   # Universal Render Pipeline/Lit
SHADER_FILEID = "4800000"
URP_ASSETVERSION_GUID = "d0353a89b1f911e48b9e16bdc9f2e058"

# Must match the colour constants in make_villagers.py exactly, so the Blender preview and the
# shipped Unity materials agree.
SKIN = (0.78, 0.58, 0.43)
SKIN_UNWELL = (0.84, 0.70, 0.60)
EYE = (0.06, 0.05, 0.05)
HAIR_DARK = (0.09, 0.07, 0.06)
HAIR_GREY = (0.70, 0.68, 0.66)
CLOTH_ELDER, TRIM_ELDER = (0.24, 0.29, 0.46), (0.58, 0.16, 0.18)
CLOTH_CHILD_A, TRIM_CHILD_A = (0.28, 0.55, 0.55), (0.92, 0.87, 0.72)
CLOTH_CHILD_B, TRIM_CHILD_B = (0.80, 0.60, 0.22), (0.52, 0.20, 0.16)

MATERIALS_LIST = [
    ("M_Villager_Eye",               EYE),            # shared by all three characters
    ("M_Villager_Elder_Skin",         SKIN),
    ("M_Villager_Elder_Skin_Unwell",  SKIN_UNWELL),
    ("M_Villager_Elder_Hair",         HAIR_GREY),
    ("M_Villager_Elder_Cloth",        CLOTH_ELDER),
    ("M_Villager_Elder_Trim",         TRIM_ELDER),
    ("M_Villager_ChildA_Skin",        SKIN),
    ("M_Villager_ChildA_Skin_Unwell", SKIN_UNWELL),
    ("M_Villager_ChildA_Hair",        HAIR_DARK),
    ("M_Villager_ChildA_Cloth",       CLOTH_CHILD_A),
    ("M_Villager_ChildA_Trim",        TRIM_CHILD_A),
    ("M_Villager_ChildB_Skin",        SKIN),
    ("M_Villager_ChildB_Skin_Unwell", SKIN_UNWELL),
    ("M_Villager_ChildB_Hair",        HAIR_DARK),
    ("M_Villager_ChildB_Cloth",       CLOTH_CHILD_B),
    ("M_Villager_ChildB_Trim",        TRIM_CHILD_B),
]


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
    - _Smoothness: 0.2
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
    os.makedirs(MATERIALS, exist_ok=True)
    results = []
    for name, (r, g, b) in MATERIALS_LIST:
        mat = os.path.join(MATERIALS, name + ".mat")
        results.append((unity_path(mat), write(mat, MATERIAL_TEMPLATE.format(
            name=name, shader_fileid=SHADER_FILEID, shader_guid=SHADER_GUID,
            urp_version_guid=URP_ASSETVERSION_GUID, r=r, g=g, b=b))))
        results.append((unity_path(mat) + ".meta",
                        write(mat + ".meta", ASSET_META.format(guid=guid_for(mat)))))

    for p, s in results:
        print(f"  {s:<9} {p}")
    print(f"\n{len(results)} files")


if __name__ == "__main__":
    main()
