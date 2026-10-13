# V14 quarter-view otter in Unity

`Assets/Prefabs/Plaza/PlazaOtter_Rigged.prefab` is the farmer otter on the V14
cutout rig, wired like the other plaza otters:

- root: `SortingGroup`, `OtterWanderAgent` (idle/walk/toys/spots/crowd),
  `OtterVisualController` (facing, depth sorting via `PlazaDepth`)
- child `Visual`: `QuarterRigView`, which builds the front (SE) and rear (NE)
  part hierarchies from `Resources/QuarterRigV14` and poses them in code.
  Heading up shows the rear view, heading left mirrors. The step rate follows
  the agent's walk speed (`walkReferenceSpeed` 1.1 u/s = the authored 0.84 s
  cycle), and walking blends in/out over 0.12 s so stopping doesn't pop.
  The part objects are rebuilt on enable and never saved; in edit mode they
  are hidden and show the idle pose.

`OtterVisualController` uses a `QuarterRigView` when the prefab has one (instead
of the sprite/Animator/frame clips). On a sit spot it sits and dozes (below);
otherwise the rig has no play actions yet, so at a toy, table, lamp or task the
otter just stands facing it.

## Actions

Actions are keyframed offsets on top of the idle pose (`HarvestKeys` in
`QuarterRigView`: hips drop, body/head/arm/tail/backpack angles, feet spread,
held-crop scale; smoothstep between keys).

- `PlayHarvest(Sprite crop)` (~2.4 s, front view, see `ArtSource/Otter/QuarterRig/harvest.gif`):
  squat with hands to the crop, two tugs, pop up holding the crop in front of
  the chest, tuck it into the backpack. The crop sprite hangs from the near hand
  (`heldCropHeight` rig px tall, kept upright) and draws behind the body once
  it goes into the backpack. `IsActing` stays true until it is back to idle.

- `SetSitting(bool)` (looping, see `ArtSource/Otter/QuarterRig/doze.gif`): this
  transform is the seat point; the bottom settles onto it and the legs stretch
  out in front (diagonally down-right, towards the camera) from under the
  belly, behind the body. Then it dozes: one nod every 3.4 s - the head sinks slowly with the
  eyes closed (`front-head-sleep`), jerks back up with a short startled blink,
  and a "z" (`fx-z`) floats up about once a second while it sinks. In the plaza,
  `OtterVisualController` calls it with `OtterWanderAgent.IsSeated` (hopped up
  on a chair, bench, log or stump and staying), so rigged otters doze on any
  sit spot.

`make_sleep_parts.py` makes `front-head-sleep.png` (the eyes painted over with
the nearest fur/muzzle and redrawn as closed lids, same size and pivot as
`front-head`) and `fx-z.png`. Rerun it after re-exporting the parts.

## Rig editor (tuning.json)

`Tools/QuarterRig/rig-editor.html` (open it directly in Chrome/Edge) previews the
rig in the four directions with the four motions (stand, walk, harvest, sit and
doze), using a port of `QuarterRigView`'s pose code, and adjusts single parts per
view: offset (rig px, y down) and rotation (degrees, clockwise) on the joint, which
carry the attached parts, and a scale on that part's sprite only. Click a part to
select it, drag to move it. "tuning.json 저장" writes the file (pick
`Assets/Resources/QuarterRigV14/tuning.json`); `QuarterRigView` reads it when it
builds the rig (Play, scene reload). Only changed parts are written:

```json
{ "rig": "QuarterRigV14", "front": [ { "part": "head", "x": 0, "y": -6, "rotation": 12, "scaleX": 1.2, "scaleY": 1.2 } ], "rear": [] }
```

`build_rig_editor.py` rebuilds the page from `rig-editor.template.html`, packing
in the parts, pivots, the bench, a carrot icon and the current `tuning.json`
(rerun after re-exporting parts). The pose constants in the template are copied
from `QuarterRigView.cs`; change both together. Checked once: the same test
tuning rendered identically in the editor and in Unity.

## Farm

`Assets/Scenes/Farm_Rigged.unity` is `Farm.unity` with a `Rig` child
(`QuarterRigView`, feet at the old sprite's bottom: local y -0.9167) on the
farmer and `FarmerOtterController.rigView` set. With a rig the controller hides
the sprite and Animator, walks/faces through the rig, and on each harvest plays
`PlayHarvest` with the slot crop's bag icon (the grown slot sprite includes the
soil). Sorting stays at `spriteSortingOrder` (2, above the crop slots). The rig
has no idle actions yet, so at waypoints/toys it just waits. The step rate
scales with the rig's world scale (farm 0.6).

`GroundShadow` uses the rig's feet and hat width (`QuarterRigView.Width`) for
rigged otters instead of the largest sprite (which would be the head part, or
the hidden old sprite).

Default standing height is ~1.67 world units (`characterScale` 0.503), matching
`PlazaSettings.otterHeight`.

## Test scene

`Assets/Scenes/Plaza_RiggedPlacement.unity` is `Plaza.unity` plus two
`PlazaOtter_Rigged` instances under `Otters` at (0, 1.5) and (1.8, 0.6), near
the initial camera focus. `PlazaController` initialises otters placed under
`Otters`, so they wander like spawned ones. Press Play.

Not yet switched over: `Otter_Farmer.asset` still points at the sprite
`PlazaOtter.prefab`. `GatheringDirector` (dinner guests) and `RegionWorkerView`
read a single `SpriteRenderer`/`Animator` from the plaza prefab, so they need
rig support before a settlement otter can use this prefab.

## Artwork

```
node Tools/QuarterRig/export-unity.cjs   # needs Playwright + local Chrome
```

`export-unity.cjs` draws each part (including the web renderer's fabric backing
and rounded trousers) from the self-contained preview onto a 1024 canvas
centred on its joint, then runs `crop_unity_parts.py`, which trims each part to
its artwork (sizes rounded to a multiple of 4 for compression) and writes a
Sprite import `.meta` with the joint as a custom pivot at PPU 200, keeping the
GUID. 16 parts went from 64 MB uncompressed to well under 1 MB on disk.
No animation frames are baked. V9 baseline files are untouched.
