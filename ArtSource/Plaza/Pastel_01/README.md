# Pastel 01

Source: the user's original coastal map, 4097 × 3984, preserved as `Plaza_Original_00_4097x3984.png`.

`Plaza_Pastel_01_4097x3984.png` uses a pixel-local color matrix: 90% saturation, then an 8% warm ivory mix. No generative redraw, blur, sharpening or resizing of this master. Shapes, line widths and the shell emblem are preserved.

Reproduce with `Tools/MapExpansion/build_pastel_map.ps1`. The runtime copy is reduced horizontally from 4097 to 4072 to match existing world coordinates; height remains 3984. Four 2036 × 1992 tiles at PPU 40 preserve the existing 101.8 × 99.6 world extent. Texture compression and mipmaps are disabled; bilinear filtering and clamp edges remain enabled.

Scene references and both settlement setup entry points use `Assets/Art/Plaza/Pastel_01`. Earlier variants are retained. No camera, buildings, movement polygons or gameplay settings were changed. The source's own resolution still limits close-up detail; this change preserves it rather than promising unlimited zoom.
