"""Crop the exported V14 rig parts to their artwork and import them as sprites.

export-unity.cjs draws every part on a 1024x1024 canvas whose centre is the
joint (2 canvas px per rig px). Only ~5% of each canvas is artwork, so this
trims each part to its alpha bounds (+ padding, size rounded up to a multiple
of 4 so block compression works) and writes a Sprite import .meta whose
custom pivot is that joint, at PPU 200 (= rig px / 100 per world unit).

Existing GUIDs are kept. Parts that are no longer 1024x1024 were already
cropped (their pivot lives in the .meta) and are skipped, so running this
twice is harmless; export-unity.cjs runs it after every export.
"""
import pathlib
import re

from PIL import Image

OUT = pathlib.Path(__file__).resolve().parents[2] / "Assets/Resources/QuarterRigV14"
CANVAS = 1024
JOINT = CANVAS // 2
PAD = 2
PPU = 200

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
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
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 9
  spritePivot: {{x: {px:.6g}, y: {py:.6g}}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
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
{platforms}  spriteSheet:
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

PLATFORM = """  - serializedVersion: 4
    buildTarget: {target}
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
"""


def round_up_within(lo, hi, multiple):
    """Grow [lo, hi) to a multiple of `multiple`, staying inside the canvas."""
    extra = -(hi - lo) % multiple
    hi = min(CANVAS, hi + extra)
    lo = max(0, lo - (multiple - (hi - lo) % multiple) % multiple)
    return lo, hi


def crop(png):
    image = Image.open(png).convert("RGBA")
    if image.size != (CANVAS, CANVAS):
        return None
    box = image.getchannel("A").getbbox()
    if box is None:
        raise SystemExit(f"{png.name} is empty")
    x0, y0, x1, y1 = box
    x0, y0, x1, y1 = max(0, x0 - PAD), max(0, y0 - PAD), min(CANVAS, x1 + PAD), min(CANVAS, y1 + PAD)
    x0, x1 = round_up_within(x0, x1, 4)
    y0, y1 = round_up_within(y0, y1, 4)
    image.crop((x0, y0, x1, y1)).save(png, optimize=True)
    w, h = x1 - x0, y1 - y0
    # Unity pivots are normalised from the bottom-left; the canvas is top-down.
    return (JOINT - x0) / w, (y1 - JOINT) / h, w, h


def main():
    platforms = "".join(PLATFORM.format(target=t) for t in ("DefaultTexturePlatform", "Standalone", "Android", "WebGL"))
    for png in sorted(OUT.glob("*.png")):
        result = crop(png)
        if result is None:
            print(f"{png.name}: already cropped")
            continue
        px, py, w, h = result
        meta = png.with_name(png.name + ".meta")
        found = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(encoding="utf-8"), re.M) if meta.exists() else None
        if found is None:
            raise SystemExit(f"{meta.name} has no guid; let Unity import the part once first")
        meta.write_text(META.format(guid=found.group(1), px=px, py=py, ppu=PPU, platforms=platforms),
                        encoding="utf-8", newline="\r\n")
        print(f"{png.name}: {w}x{h}, pivot ({px:.3f}, {py:.3f})")


if __name__ == "__main__":
    main()
