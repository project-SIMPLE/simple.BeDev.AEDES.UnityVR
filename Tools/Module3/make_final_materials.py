#!/usr/bin/env python3
"""
Per-asset materials for the Module 3 final art (make_final_models.py), one
flat colour each on URP/Lit -- matching
Asset Brief Sec.2: "one material per asset, not one per part", no PBR sets,
toon/baked-lighting style.

This replaces the single shared M_Module3Placeholder.mat that the greybox
pass used for all nine props (Module3PlaceholderPrefabs.cs Entry.Material now
points each entry at its own of these). M_Module3Placeholder.mat is left in
place -- it is still what any future greybox placeholder should bind to.

Run after make_final_models.py:
    python3 Tools/Module3/make_final_materials.py

GUIDs are md5("aedes-m3-final:" + unity asset path), the same scheme as
make_unity_assets.py (a different salt, so they cannot collide with the
placeholder material's GUID) -- idempotent, so two people generating these
independently get the same GUIDs.
"""

import hashlib, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "Assets")
TEAM = os.path.join(ASSETS, "1.TeamWorkspace", "Team Assets")
MATERIALS = os.path.join(TEAM, "Materials")

# Universal Render Pipeline/Lit, like the rest of Team Assets and like M_Module3Placeholder.mat now
# is. NOT the project's SHD_SimpleLit_Static graph: that toon graph takes its colour from a gradient
# texture (_Texture_01) and ignores _BaseColor, so a flat colour on it renders solid black in the game
# (a black net standing on a sleeping patient). Module3's PlaceholderAssetTests fails any prop that
# sits on it without a gradient texture. See the note in make_unity_assets.py.
SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"   # Universal Render Pipeline/Lit
SHADER_FILEID = "4800000"
URP_ASSETVERSION_GUID = "d0353a89b1f911e48b9e16bdc9f2e058"

# Same colours as COLOR in make_final_models.py, so the Blender preview and
# the shipped Unity material agree. Material name follows an asset's own
# name (M_ prefix, Sec.2 naming table) rather than the SM_ mesh name.
MATERIALS_LIST = [
    ("M_ElectricFan",          (0.88, 0.86, 0.80)),
    ("M_WindowScreen",         (0.40, 0.33, 0.24)),   # shared by intact + torn (Asset Brief A3)
    ("M_MosquitoNet_RolledUp", (0.90, 0.88, 0.82)),
    ("M_RepellentBottle",      (0.82, 0.52, 0.16)),
    ("M_DrinkingVessel",       (0.87, 0.85, 0.79)),
    ("M_Cloth",                (0.52, 0.68, 0.66)),
    ("M_Torch",                (0.11, 0.11, 0.12)),
    ("M_HealthCentre",         (0.90, 0.88, 0.82)),
]


def unity_path(abs_path):
    return "Assets/" + os.path.relpath(abs_path, ASSETS).replace(os.sep, "/")


def guid_for(abs_path):
    h = hashlib.md5(("aedes-m3-final:" + unity_path(abs_path)).encode())
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
