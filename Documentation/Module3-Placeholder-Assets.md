# Module 3 — §4 asset gap: from greybox to final art

Implements the **Unblocking pattern** from `Module3-Roadmap.md` §4:

> Commit a greybox placeholder prefab at the final path and name for every asset above, on day one
> of Phase 0. Code binds to the path and the sockets; art replaces the mesh inside the prefab later.
> Nothing in Phases 1–5 ever waits for a model, and the swap is a one-file change with no scene churn.

**Status (2026-09-29): the swap described above has happened, and a detail pass followed it.**
All nine assets are final meshes from `Tools/Module3/make_final_models.py`, each with its own
material from `Tools/Module3/make_final_materials.py`, built exactly per the "Handing these to
whoever makes the real models" section below: same paths, same node names, same `Socket_*`
empties, same origins, same `.fbx.meta` GUIDs. The detail pass (see *The detail pass* below)
raised the triangle budgets, gave every prop several colours through one shared palette texture,
and made the window screens see-through. `Tools/Module3/make_placeholders.py` (the original
flat-grey greybox generator) and `M_Module3Placeholder.mat` are left in the repo for any *future*
placeholder — the prefab builder still supports binding a still-greybox entry to it — but none of
the nine current assets use them any more.

The **dimensions, origins, pivots and socket names are still the deliverable that matters most**,
because code and scenes bind to them, and the final meshes keep every one exactly as the greybox
pass fixed them. What changed is only what sits inside that envelope.

Everything below was verified by importing into Unity 6000.3.8f1 and inspecting the built prefabs —
not asserted from the Blender side. That distinction mattered; see *Axis and unit traps*.

---

## What exists

| Prefab (bind to this) | Source mesh | Tris / budget | Size (m, Unity XYZ) | Sockets |
|---|---|---|---|---|
| `Prefabs/Furniture/PF_ElectricFan.prefab` | `SM_ElectricFan` | 4700 / 5000 | 0.306 × 0.453 × 0.21 | `Socket_Mount` |
| `Prefabs/Buildings/PF_WindowScreen.prefab` | `SM_WindowScreen` | 1408 / 2000 | 1.5 × 1.0 × 0.04 | `Socket_Mount` |
| `Prefabs/Buildings/PF_WindowScreenTorn.prefab` | `SM_WindowScreenTorn` | 2152 / 2500 | 1.5 × 1.0 × 0.086 | `Socket_Mount` |
| `Prefabs/Props/PF_MosquitoNet_RolledUp.prefab` | `SM_MosquitoNet_RolledUp` | 2824 / 3000 | 0.310 × 0.591 × 0.317 | `Socket_Mount`, `Socket_Hook` |
| `Prefabs/Props/PF_MosquitoNet_Deployed.prefab` | `SM_MosquitoNet` *(existing art)* | 298 | 1.56 × 1.383 × 1.766 | — |
| `Prefabs/Props/PF_RepellentBottle.prefab` | `SM_RepellentBottle` | 1904 / 2000 | 0.08 × 0.191 × 0.06 | `Socket_Grip` |
| `Prefabs/Props/PF_DrinkingVessel.prefab` | `SM_DrinkingVessel` | 1164 / 1500 | 0.119 × 0.107 × 0.087 | `Socket_Grip` |
| `Prefabs/Props/PF_Cloth.prefab` | `SM_Cloth` | 1516 / 2000 | 0.4 × 0.05 × 0.26 | `Socket_Grip` |
| `Prefabs/Props/PF_Torch.prefab` | `SM_Torch` | 2148 / 2500 | 0.066 × 0.166 × 0.07 | `Socket_Grip`, `Socket_Light` |
| `Prefabs/Buildings/PF_HealthCentre.prefab` | `SM_HealthCentre` | 1796 / 3000 | 12.6 × 3.65 × 10.3 | `Socket_Entrance` |

Paths are relative to `Assets/1.TeamWorkspace/Team Assets/`. Meshes live at
`Team Assets/Models/<name>/<name>.fbx`, following the existing folder-per-model convention.

Each asset has its own material (`M_ElectricFan`, `M_WindowScreen` — shared by the intact and
torn screen frames, per Asset Brief A3 — `M_MosquitoNet_RolledUp`, `M_RepellentBottle`,
`M_DrinkingVessel`, `M_Cloth`, `M_Torch`, `M_HealthCentre`), on URP/Lit (see the caveat below for
why not the toon graph) — Asset Brief §2: "one material per asset, not one per part", no PBR sets.
Since the detail pass every one of them samples the same 8×8 swatch texture,
`Team Assets/Textures/T_M3_PropPalette.png`, and each part of a mesh has all of its UVs parked on
the centre of one swatch: that is how a fan is cream, blue and grey in one draw call. The swatch
list lives in `Tools/Module3/m3_palette.py` (append, never reorder — the index is baked into the
committed FBX UVs). The one exception is the screens' netting, `Screen_Mesh`, a child object that
gets `M_WindowScreenMesh` — an alpha-cutout, two-sided wire grid (`T_M3_ScreenMesh.png`, one tile
per 15 mm) — so a screen is something you see through, not a board over the window.
`M_Module3Placeholder.mat` still exists for any future greybox placeholder but nothing here
references it any more.

Every asset maps to a Phase 1 player action, which is why this is the list and not a longer one:

| Action | Asset |
|---|---|
| `SetFan(householdId)` | `PF_ElectricFan` |
| `RepairScreen(householdId)` | `PF_WindowScreenTorn` → `PF_WindowScreen` |
| `PutUpNet(personId)` | `PF_MosquitoNet_RolledUp` → `PF_MosquitoNet_Deployed` |
| `GiveRepellent(personId)` | `PF_RepellentBottle` |
| `BringWater(personId)` | `PF_DrinkingVessel` |
| `HelpRest(personId)` | `PF_Cloth` |
| `ReferToHealthCentre(personId)` | `PF_HealthCentre` *(provisional)* |
| night visits | `PF_Torch` |

There is deliberately no container-clearing prop: §5 is explicit that clearing containers is
"not a button, not a task, not a score".

---

## Dimensions measured from existing assets

The screens and the net are not invented sizes. They were measured off the shipped art so Module 3
fits the world it reuses (§11: "feel like the same world the student has already worked in").

| Source asset | Measurement | Used for |
|---|---|---|
| `SM_Windows/SM_WindowRight.fbx` + `SM_WindowLeft.fbx` | two 0.75 × 1.00 m leaves meeting on the centre line → **1.50 × 1.00 m opening**, reveal 0.1202 m deep | screen outer size; screen depth held to 0.04 m so it sits inside the reveal |
| `SM_MosquitoNet/SM_MosquitoNet.fbx` | dome 1.5596 × 1.7661 footprint, **1.391 m tall**, base on the ground plane, centred in plan | the rolled-up variant shares that origin and canopy height, so the two net prefabs swap at one transform |
| `SM_Bed/SM_Bed.fbx` | 1.5961 × 1.9368 × 1.1807 m | reference only — the net is sized off the net, not the bed |

Re-measure anything at any time:

```bash
blender -b --factory-startup --python Tools/Module3/measure_reference.py -- <paths to .fbx>
```

---

## Conventions, and two judgement calls

The §4 contract is met as written: FBX, metres, Y-up / −Z forward, transforms applied, one material
per prop, `SM_`/`PF_`/`M_` naming, sockets as empties, `.meta` files committed. Two points needed a
decision:

**1. Origin at the base; the grip is a socket.** §4 says "origin at the floor contact point (props:
at the natural grip point)". These props both *sit* in the world and get *picked up*, and a grip
origin makes every placement float. So all props keep a base origin — they drop onto a surface at
y = 0 — and the grip is carried by `Socket_Grip`, which is what sockets are for. Code should attach
to the socket, so this stays true even if a prop is later hand-authored with a grip origin.

**2. The deployed net reuses existing art.** Only the rolled-up state was genuinely missing, so only
it was modelled. `PF_MosquitoNet_Deployed` is a thin prefab over the shipped `SM_MosquitoNet` mesh
with its own materials intact, existing so the two states are addressed by symmetric paths.
**`PF_MosquitoNet` itself is untouched.**

### Sockets

| Socket | Meaning |
|---|---|
| `Socket_Mount` | where the object attaches to floor, bed or window |
| `Socket_Grip` | the hand |
| `Socket_Hook` | ceiling tie point of the rolled-up net |
| `Socket_Light` | torch beam origin — **its forward is the beam**, so a `Light` parented with identity rotation aims correctly (verified: forward = `(0, 1, 0)`) |
| `Socket_Entrance` | health-centre door — forward faces **out**, so a player arriving along it walks in (verified: forward = `(0, 0, 1)`) |

A socket that encodes no direction is left at identity rotation.

### The fan blade turns

§6 makes the fan the realistic alternative where there is no screen, so the blade has to actually
move. Hierarchy is `Fan_Base → Fan_Head → Fan_Blade`, and `Fan_Blade` is a separate object whose
origin sits exactly on the spin axis at local `(0, 0.3, 0.035)` with identity rotation and unit
scale. **Animate its local Z.** The fan faces Unity +Z.

---

## Regenerating

```bash
# 1. meshes — writes Team Assets/Models/<name>/<name>.fbx, enforces the tri budgets
#    (add "-- --out DIR" to stage them elsewhere first, "-- --only SM_Torch" for one asset)
blender -b --factory-startup --python Tools/Module3/make_final_models.py

# 2. the palette + screen-mesh textures and the per-asset materials that sample them
python3 Tools/Module3/make_final_materials.py

# 3. prefabs, in Unity:   AEDES ▸ Module 3 ▸ Build Placeholder Prefabs
#    check the contract:   AEDES ▸ Module 3 ▸ Verify Placeholder Contract
```

(The menu items keep their original names — they still build/verify prop prefabs against the same
§4 contract — but they now assign each asset's own material rather than forcing the shared grey
one, and a named child such as `Screen_Mesh` its own part material (`Entry.PartMaterials`). `Tools/Module3/make_placeholders.py` + `make_unity_assets.py` are the original greybox
generators; keep them for any *future* placeholder, but they are not part of regenerating the nine
current assets.)

Step 3 is `Assets/Editor/Module3PlaceholderPrefabs.cs`. It is idempotent — it overwrites the prefabs
in place, so prefab GUIDs and every scene reference to them survive a re-run. It reports, per prefab,
the triangle count against budget, the measured size against the expected size, and whether the
expected sockets actually survived the FBX import, because a silently-dropped socket would strand the
code that binds to it.

Headless equivalent, useful from CI or a script:

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath . \
  -executeMethod Module3PlaceholderPrefabs.Build -logFile /tmp/m3.log
```

Note that Unity batchmode exits 0 even when work failed — read the `[Module3]` block in the log
rather than trusting the exit code.

Mesh GUIDs are `md5("aedes-m3-placeholder:" + asset path)` (unchanged since the greybox pass —
`make_final_models.py` overwrites the `.fbx` in place and never touches `.fbx.meta`). Material GUIDs
are `md5("aedes-m3-final:" + asset path)`, a different salt so they cannot collide with
`M_Module3Placeholder`'s. Both are idempotent; two people regenerating independently get identical
GUIDs. Blender is only needed to *regenerate*; the FBXes are committed.

---

## The detail pass

The first final meshes were built from 14–18-segment primitives with one flat colour each, and
in the game they read as faceted greybox with a coat of paint: box fan blades with guard bars
running through them, a bullet-shaped net, a cup whose handle was a ring lying flat against its
side, an opaque brown board for a window screen, a white block for the health centre. The detail
pass (2026-09-29) reworked all nine in `make_final_models.py` without touching the contract:

- **Geometry.** Lathes run at 28–36 segments; edges get a small two-segment bevel where it
  helps (not on the fan guard's 36 wires or the torch's ribs — a bevel there triples the count for
  nothing) and everything is shaded smooth-by-angle so flat faces stay flat. A first cut ran at
  40–56 segments and about double these counts; profiling showed the extra triangles on guard
  rings, wires and lathe walls that a Quest lens cannot resolve at arm's length, so they went. The fan has a real
  wire guard (two rings, a mid ring, a badge plate, 24 front and 12 back wires), three twisted,
  cambered blades on a domed hub, a lathed pedestal with three push buttons and a yoke with pivot
  pins. The rolled net is a rippled lathe (the ripple twists as it climbs) with two tie cords,
  knots and loose ends. The cup is enamelware with a blue rim, water inside and a D handle that
  stands off the wall. The cloth is a checked pha khao ma with soft folds. The torch has a
  knurled grip, a silver bezel and a red button. The health centre has a hipped roof, a porch on
  columns, twelve framed windows, a painted door and a sign with a red cross.
- **Colour.** One material per prop still, but each samples the shared palette texture (above),
  so parts carry their own colour. The palette is point-filtered, uncompressed and mip-free so a
  swatch centre samples exactly the listed sRGB value.
- **Screens.** The netting is its own child object, `Screen_Mesh`, on an alpha-cutout wire-grid
  material with coverage-preserving mipmaps; the torn variant's right bay is a lattice with a
  ragged hole and two flaps curling into the room, which is what sets its 0.086 m depth.
- **Budgets** went from 500–3000 to 1500–5000 per prop (table above); the
  `PlaceholderAssetTests` ceiling of 8000 per prefab is unchanged and every prop is under it.
  A house plot with its four villagers is about 17k triangles, of which the props are 3–8k.
- **Sizes** that moved: the cup is 0.119 m wide because its handle now sticks out; the torn
  screen's flaps reach 0.086 m instead of 0.104 m. Everything else is within 2 cm of before, and
  `Verify Placeholder Contract` reports 0 problems.

Checked in the running game with the playtest harness (a throwaway extract of `Assets/Playtest`,
not committed): every prop and the three villager bodies were spawned in a row and photographed
from fixed cameras before and after, then the village scene itself (a fan, a torn screen and a
rolled net in a real house) — the shots were compared by eye, and the palette and cutout
materials render as intended under the scene's lighting.

### Drawing less of it

Two scene-level changes landed with the second cut, because they save more per frame than any
mesh edit:

- **Distance culling.** The village is built at runtime, so nothing can be baked for occlusion
  culling, and every villager and prop inside the view cone was drawn whether or not a wall was
  in the way. `M3DistanceCulling` (added to the builder's GameObject on `Awake`) sets the main
  camera's per-layer cull distance for the `DistanceCulled` layer (TagManager user layer 10) to
  22 m, spherical. The builder puts villagers and beds on that layer and `HouseholdView.Place`
  puts the fan, screens and nets on it; houses, palms, the ground and the yard jars (outdoors,
  cheap, and they would pop into view on the lane) stay on Default. Only renderer GameObjects are moved, never colliders, so pointing and teleporting are
  untouched. `DistanceCullingTests` fails if the layer goes missing, since the runtime would
  silently draw everything again.
- **Shadows on Android.** `Performance URP Config` (the Low quality level, which Android
  defaults to) had a 8192×8192 main-light shadow map over 30 m; it is now 2048 over 20 m. See the
  commit for the exact fields. Modules 1 and 2 share the asset, so they get the same saving.

## Axis and unit traps (read before touching the exporter)

Four Unity round-trips were needed to get this right, and **a Blender round-trip cannot catch any of
it** — Blender reads its own conventions back consistently, so the FBXes looked perfect while being
wrong in Unity. Always verify in Unity.

1. **`UnitScaleFactor`.** The repo's existing art declares `100.0` (centimetres). Exporting with
   `apply_scale_options='FBX_SCALE_NONE'` declares `1.0`, and Unity then scales everything by 0.01 —
   a 1.5 m screen arrives 15 mm wide. Fixed with **`apply_scale_options='FBX_SCALE_UNITS'`**, which
   puts the unit scaling in the FBX header and leaves transforms in metres.

2. **`bake_space_transform` mangles hierarchies.** It bakes the Z-up → Y-up conversion into vertex
   data, which is what gives an identity root — but it applies the rotation inconsistently across a
   parented hierarchy. Tested directly: `Fan_Blade` picked up a spurious 90° X rotation while its
   sibling `Fan_Head` did not, inflating the fan from 0.306 × 0.453 × 0.21 to 0.306 × 0.503 × 0.531.
   **Do not enable it on anything with children.**

3. **So the conversion is done explicitly**, in `convert_to_yup()`: mesh data and local offsets are
   rotated into Unity's axes in Blender, and the FBX is then exported with the exporter's own
   conversion disabled (`axis_forward='-Y', axis_up='Z'`, which makes its conversion matrix the
   identity). Meshes keep identity rotation; the rotation lives in the vertices. This is exact and
   hierarchy-safe.

4. **Unity still adds −90° X to the root node** of a Z-up-declared FBX. Since the data is already
   converted, that rotation is a leftover, so the prefab builder sets the root back to identity —
   matching the rest of the repo's art. The size assertion in `Verify` is the guard: if the importer
   ever stops adding it, the reported bounds change and `Verify` says so.

---

## How the final art was actually delivered

This section originally briefed whoever would replace the placeholders; it is kept as a record of
what happened, since the next asset (or the next redo of one of these) follows the same recipe.

1. Model the real asset to the dimensions in the table above — `make_final_models.py` builds each
   one procedurally in Blender (lathed profiles via `bmesh.ops.spin` for bottles/vessels/the torch/
   the fan's pedestal, an extruded flat polygon for the fan blade and the fan cage rings, a small
   angle-limited bevel on every part for a toon-friendly edge highlight) rather than by hand, but the
   constraint is the same either way: match the envelope, don't invent it.
2. Export over `Team Assets/Models/<name>/<name>.fbx`, keeping the **node names**
   (`Fan_Base` / `Fan_Head` / `Fan_Blade`) and the **`Socket_*` empties** at the same points.
3. **Keep the existing `.fbx.meta`** — that is what preserves the GUID, and with it every reference.
   Confirmed: `git status` after regenerating shows only the nine `.fbx` files as modified, no
   `.meta` changes.
4. Re-run *Build Placeholder Prefabs*, which now assigns each asset's own material
   (`make_final_materials.py`) instead of `M_Module3Placeholder`.

No prefab path changes, no scene edits, no re-binding in code — confirmed by `Verify Placeholder
Contract` reporting 0 problems after the swap.

The one thing this recipe could not fully honour: the §4 routing says the fan, screens and net
variants "must be modelled by hand, because they have to fit or move". They are still procedural
Blender geometry, not hand-sculpted, but they *are* built to the exact measured envelope (window
opening, `SM_MosquitoNet`'s canopy) rather than eyeballed, which is the property that mattered.

---

## Caveats

- **These materials are URP/Lit, not the project's `SHD_SimpleLit_Static` graph.** That toon graph
  takes its colour from a gradient texture (`_Texture_01`) and ignores `_BaseColor`, so a flat colour
  on it renders solid black in the game — the black fan and rolled net seen in play before
  `M_Module3Placeholder` was moved to URP/Lit. Blender, the inspector and the prefab builder all
  render nothing and so all looked fine; only playing the scene showed it. `PlaceholderAssetTests`
  now fails any Module 3 prop on that graph without a gradient texture. `make_final_materials.py`
  writes URP/Lit materials the same way `make_unity_assets.py` does for the placeholder.
- **The health centre is provisional.** §13 item 4 has not resolved whether the player travels there;
  referral is modelled as an off-screen outcome first. It is crude massing on purpose, so a travel
  loop can be greyboxed the day that question is answered. It is the one asset whose dimensions carry
  no authority.
- **Window fit is inferred from the shutter leaves**, not from a hole measured in the house mesh —
  the house is a single mesh, so the opening cannot be isolated. The leaves occupy the same aperture
  a screen would, so it is the right reference, but the first person to place `PF_WindowScreen` in a
  real Lao house prefab should eyeball it and adjust `WINDOW_W` / `WINDOW_H` in
  `make_final_models.py` if it is off.
- **Screen depth** is 0.04 m against the 0.1202 m reveal; the torn variant's peeled flaps reach
  0.099 m, still inside it.
- **Nothing here is in a scene yet.** These are prefabs and meshes only, which is the point — §6.1
  of the roadmap asks for the M3 neighbourhood to be built from prefabs placed by a builder script
  driven by a data asset, not by hand-dragging into a scene file.
