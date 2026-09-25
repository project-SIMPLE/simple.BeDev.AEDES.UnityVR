# Module 3 — greybox placeholder assets

Implements the **Unblocking pattern** from `Module3-Roadmap.md` §4:

> Commit a greybox placeholder prefab at the final path and name for every asset above, on day one
> of Phase 0. Code binds to the path and the sockets; art replaces the mesh inside the prefab later.
> Nothing in Phases 1–5 ever waits for a model, and the swap is a one-file change with no scene churn.

These are **stand-ins**. The silhouettes are disposable. The **dimensions, origins, pivots and socket
names are the deliverable**, because code and scenes bind to them.

Everything below was verified by importing into Unity 6000.3.8f1 and inspecting the built prefabs —
not asserted from the Blender side. That distinction mattered; see *Axis and unit traps*.

---

## What exists

| Prefab (bind to this) | Source mesh | Tris / budget | Size (m, Unity XYZ) | Sockets |
|---|---|---|---|---|
| `Prefabs/Furniture/PF_ElectricFan.prefab` | `SM_ElectricFan` | 664 / 3000 | 0.306 × 0.453 × 0.21 | `Socket_Mount` |
| `Prefabs/Buildings/PF_WindowScreen.prefab` | `SM_WindowScreen` | 60 / 500 | 1.5 × 1.0 × 0.04 | `Socket_Mount` |
| `Prefabs/Buildings/PF_WindowScreenTorn.prefab` | `SM_WindowScreenTorn` | 96 / 500 | 1.5 × 1.0 × 0.099 | `Socket_Mount` |
| `Prefabs/Props/PF_MosquitoNet_RolledUp.prefab` | `SM_MosquitoNet_RolledUp` | 224 / 1500 | 0.32 × 0.589 × 0.32 | `Socket_Mount`, `Socket_Hook` |
| `Prefabs/Props/PF_MosquitoNet_Deployed.prefab` | `SM_MosquitoNet` *(existing art)* | 298 | 1.56 × 1.383 × 1.766 | — |
| `Prefabs/Props/PF_RepellentBottle.prefab` | `SM_RepellentBottle` | 180 / 1500 | 0.071 × 0.191 × 0.06 | `Socket_Grip` |
| `Prefabs/Props/PF_DrinkingVessel.prefab` | `SM_DrinkingVessel` | 120 / 1500 | 0.084 × 0.11 × 0.084 | `Socket_Grip` |
| `Prefabs/Props/PF_Cloth.prefab` | `SM_Cloth` | 36 / 1500 | 0.4 × 0.06 × 0.26 | `Socket_Grip` |
| `Prefabs/Props/PF_Torch.prefab` | `SM_Torch` | 208 / 1500 | 0.064 × 0.171 × 0.066 | `Socket_Grip`, `Socket_Light` |
| `Prefabs/Buildings/PF_HealthCentre.prefab` | `SM_HealthCentre` | 84 / 2000 | 12.6 × 3.65 × 10.3 | `Socket_Entrance` |

Paths are relative to `Assets/1.TeamWorkspace/Team Assets/`. Meshes live at
`Team Assets/Models/<name>/<name>.fbx`, following the existing folder-per-model convention.

All greybox assets share one material, `Team Assets/Materials/M_Module3Placeholder.mat` — flat grey
on the project's own `SHD_SimpleLit_Static` graph with no texture assigned, so a placeholder is
unmistakable while still rendering correctly in URP.

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
# 1. meshes — writes Team Assets/Models/<name>/<name>.fbx, enforces the §4 tri budgets
blender -b --factory-startup --python Tools/Module3/make_placeholders.py

# 2. .meta files with stable GUIDs, plus the placeholder material
python3 Tools/Module3/make_unity_assets.py

# 3. prefabs, in Unity:   AEDES ▸ Module 3 ▸ Build Placeholder Prefabs
#    check the contract:   AEDES ▸ Module 3 ▸ Verify Placeholder Contract
```

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

GUIDs are `md5("aedes-m3-placeholder:" + asset path)`, so regenerating is idempotent and two people
generating independently get identical GUIDs. Blender is only needed to *regenerate*; the FBXes are
committed.

---

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

## Handing these to whoever makes the real models

Replacing a placeholder is deliberately a one-file change:

1. Model the real asset to the dimensions in the table above.
2. Export over `Team Assets/Models/<name>/<name>.fbx`, keeping the **node names**
   (`Fan_Base` / `Fan_Head` / `Fan_Blade`) and the **`Socket_*` empties** at the same points.
3. **Keep the existing `.fbx.meta`** — that is what preserves the GUID, and with it every reference.
4. Re-run *Build Placeholder Prefabs*, then assign the real material in place of
   `M_Module3Placeholder`.

No prefab path changes, no scene edits, no re-binding in code.

The §4 routing still stands: the fan, the screens and the net variants must be modelled **by hand**,
because they have to fit or move. The bottle, vessel, cloth and torch are the ones that can be
AI-generated or bought, since they only sit on a surface — and each now has a placeholder giving the
exact size to hit.

---

## Caveats

- **Greybox, not art.** Primitive-built stand-ins. Do not ship them.
- **The health centre is provisional.** §13 item 4 has not resolved whether the player travels there;
  referral is modelled as an off-screen outcome first. It is crude massing on purpose, so a travel
  loop can be greyboxed the day that question is answered. It is the one asset whose dimensions carry
  no authority.
- **Window fit is inferred from the shutter leaves**, not from a hole measured in the house mesh —
  the house is a single mesh, so the opening cannot be isolated. The leaves occupy the same aperture
  a screen would, so it is the right reference, but the first person to place `PF_WindowScreen` in a
  real Lao house prefab should eyeball it and adjust `WINDOW_W` / `WINDOW_H` in
  `make_placeholders.py` if it is off.
- **Screen depth** is 0.04 m against the 0.1202 m reveal; the torn variant's peeled flaps reach
  0.099 m, still inside it.
- **Nothing here is in a scene yet.** These are prefabs and meshes only, which is the point — §6.1
  of the roadmap asks for the M3 neighbourhood to be built from prefabs placed by a builder script
  driven by a data asset, not by hand-dragging into a scene file.
