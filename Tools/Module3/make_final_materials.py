#!/usr/bin/env python3
"""
Per-asset materials for the Module 3 props (make_final_models.py), and the two textures they
sample. Still one material per prop (Asset Brief Sec.2), on URP/Lit like the rest of Team
Assets -- what changed in the detail pass is that a prop is no longer one flat colour:

  * T_M3_PropPalette.png is an 8x8 grid of flat colour swatches (Tools/Module3/m3_palette.py is
    the single source of truth for them). make_final_models.py parks each part's UVs on the
    centre of a swatch, so the fan's body, blades and guard wire are three colours in one
    material. The texture is point-filtered with no mipmaps and left uncompressed, so a swatch
    centre samples exactly the colour in the palette file.
  * T_M3_ScreenMesh.png is a tiny tileable wire-grid with alpha. M_WindowScreenMesh puts it on
    the screens' Screen_Mesh child as an alpha-cutout, two-sided material, so the screen is
    something you see through rather than a board over the window. Mipmaps preserve coverage,
    so the netting does not fade out at a distance.

Run after make_final_models.py:
    python3 Tools/Module3/make_final_materials.py

Why URP/Lit and not the project's SHD_SimpleLit_Static graph: that toon graph takes its colour
from a gradient texture (_Texture_01) and ignores _BaseColor, so a flat colour on it renders
solid black in the game. Module3's PlaceholderAssetTests fails any prop that sits on it without
a gradient texture. See the note in make_unity_assets.py.

GUIDs are md5("aedes-m3-final:" + unity asset path), the same scheme as make_unity_assets.py
(a different salt, so they cannot collide with the placeholder material's GUID) -- idempotent,
so two people generating these independently get the same GUIDs.
"""

import hashlib, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import m3_palette as PAL

REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "Assets")
TEAM = os.path.join(ASSETS, "1.TeamWorkspace", "Team Assets")
MATERIALS = os.path.join(TEAM, "Materials")
TEXTURES = os.path.join(TEAM, "Textures")

SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"   # Universal Render Pipeline/Lit
SHADER_FILEID = "4800000"
URP_ASSETVERSION_GUID = "d0353a89b1f911e48b9e16bdc9f2e058"

PALETTE_PNG = os.path.join(TEXTURES, PAL.TEXTURE + ".png")
SCREEN_PNG = os.path.join(TEXTURES, "T_M3_ScreenMesh.png")

# One material per prop. All of them sample the palette; the colour lives in the mesh UVs now,
# so _BaseColor is white. Material name follows the asset's own name (M_ prefix).
PALETTE_MATERIALS = [
    "M_ElectricFan",
    "M_WindowScreen",          # shared by intact + torn frames (Asset Brief A3)
    "M_MosquitoNet_RolledUp",
    "M_RepellentBottle",
    "M_DrinkingVessel",
    "M_Cloth",
    "M_Torch",
    "M_HealthCentre",
]

# The wire grid: SCREEN_TILE px per tile, WIRE px of wire along each edge of the tile. One
# tile is one SCREEN_CELL (15 mm) of screen in make_final_models.py.
SCREEN_TILE, WIRE = 32, 3
WIRE_RGB = (0.24, 0.26, 0.27)


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


def write_bytes(path, data):
    existing = None
    if os.path.exists(path):
        with open(path, "rb") as f:
            existing = f.read()
    if existing == data:
        return "unchanged"
    with open(path, "wb") as f:
        f.write(data)
    return "written" if existing is None else "updated"


def screen_rows():
    rows = []
    r, g, b = (round(c * 255) for c in WIRE_RGB)
    for py in range(SCREEN_TILE):
        row = bytearray()
        for px in range(SCREEN_TILE):
            on = px < WIRE or py < WIRE
            row += bytes((r, g, b, 255 if on else 0))
        rows.append(bytes(row))
    return rows


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
  m_ValidKeywords:{keywords}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: {double_sided_gi}
  m_CustomRenderQueue: {queue}
  stringTagMap:{tags}
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {texture}
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
        m_Texture: {texture}
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
    - _AlphaClip: {alphaclip}
    - _AlphaToMask: {alphaclip}
    - _Blend: 0
    - _BlendModePreserveSpecular: 1
    - _BumpScale: 1
    - _ClearCoatMask: 0
    - _ClearCoatSmoothness: 0
    - _Cull: {cull}
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
    - _Smoothness: {smoothness}
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

# Texture importer, cloned from Assets/Resources/Module3/T_M3_HouseDetail.png.meta and reduced to
# the fields that differ between the two textures here.
TEXTURE_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: {mips}
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: {preserve_coverage}
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: {filter}
    aniso: {aniso}
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: {wrap}
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: {alpha_is_transparency}
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: {compression}
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def texture_ref(png_path):
    return "{fileID: 2800000, guid: %s, type: 3}" % guid_for(png_path)


def material_text(name, png_path, rgb=(1, 1, 1), cutout=False, smoothness=0.2):
    r, g, b = rgb
    return MATERIAL_TEMPLATE.format(
        name=name, shader_fileid=SHADER_FILEID, shader_guid=SHADER_GUID,
        urp_version_guid=URP_ASSETVERSION_GUID,
        texture=texture_ref(png_path),
        keywords="\n  - _ALPHATEST_ON" if cutout else " []",
        tags="\n    RenderType: TransparentCutout" if cutout else " {}",
        queue=2450 if cutout else -1,
        double_sided_gi=1 if cutout else 0,
        alphaclip=1 if cutout else 0,
        cull=0 if cutout else 2,
        smoothness=smoothness,
        r=r, g=g, b=b)


def main():
    os.makedirs(MATERIALS, exist_ok=True)
    os.makedirs(TEXTURES, exist_ok=True)
    results = []

    # --- textures ---
    from io import BytesIO
    import tempfile
    tmp = tempfile.NamedTemporaryFile(suffix=".png", delete=False).name
    PAL.write_png(tmp, PAL.SIZE, PAL.SIZE, PAL.png_rows())
    results.append((unity_path(PALETTE_PNG), write_bytes(PALETTE_PNG, open(tmp, "rb").read())))
    results.append((unity_path(PALETTE_PNG) + ".meta", write(PALETTE_PNG + ".meta", TEXTURE_META.format(
        guid=guid_for(PALETTE_PNG), mips=0, preserve_coverage=0, max_size=128,
        filter=0, aniso=1, wrap=1, alpha_is_transparency=0, compression=0))))

    PAL.write_png(tmp, SCREEN_TILE, SCREEN_TILE, screen_rows())
    results.append((unity_path(SCREEN_PNG), write_bytes(SCREEN_PNG, open(tmp, "rb").read())))
    results.append((unity_path(SCREEN_PNG) + ".meta", write(SCREEN_PNG + ".meta", TEXTURE_META.format(
        guid=guid_for(SCREEN_PNG), mips=1, preserve_coverage=1, max_size=32,
        filter=2, aniso=4, wrap=0, alpha_is_transparency=1, compression=0))))
    os.remove(tmp)

    # --- materials ---
    for name in PALETTE_MATERIALS:
        mat = os.path.join(MATERIALS, name + ".mat")
        results.append((unity_path(mat), write(mat, material_text(name, PALETTE_PNG))))
        results.append((unity_path(mat) + ".meta", write(mat + ".meta", ASSET_META.format(guid=guid_for(mat)))))

    mat = os.path.join(MATERIALS, "M_WindowScreenMesh.mat")
    results.append((unity_path(mat), write(mat, material_text("M_WindowScreenMesh", SCREEN_PNG, cutout=True, smoothness=0.1))))
    results.append((unity_path(mat) + ".meta", write(mat + ".meta", ASSET_META.format(guid=guid_for(mat)))))

    for p, s in results:
        print(f"  {s:<9} {p}")
    print(f"\n{len(results)} files")


if __name__ == "__main__":
    main()
