# Thin-line map integration preview

The user-approved image was edited with the built-in image_gen tool. The prompt was to preserve the coastal map's colors, paths, framing and lower-right shell starting point while reducing outline thickness to match the thin-line reference.

`approved-map.png` is the actual generated source: **1271 × 1237**. `Plaza_ThinLine_4097x3984.png` has the requested delivery dimensions, but is bicubic resampling, **not a native 4K redraw**. The 8144 × 7968 runtime master and its four 4072 × 3984 tiles are also resampled. They do not add detail and do not guarantee sharp maximum zoom. This is an integration preview, not fulfillment of the native-detail requirement.

Run `Tools/MapExpansion/package_thin_map.ps1` to reproduce the export and tiles. Unity uses `Assets/Art/Plaza/ThinLineMap`, with uncompressed, non-mipmapped, bilinear, clamp textures and 4096 texture limits. The existing 101.8 × 99.6 world extent, camera zoom, building positions and movement polygons are unchanged. Visual alignment of the newly generated terrain with movement polygons still needs in-game review.

Previous map assets remain in `Assets/Art/Plaza/AlignedOtterMap`. The scene references and both settlement setup entry points now use the new folder.
