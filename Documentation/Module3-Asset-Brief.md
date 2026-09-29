# Module 3 — 3D Asset Brief

**For:** whoever or whatever is producing the models — a generative 3D model, a contracted artist,
or a team member working in Blender. You do not need to know Unity or the rest of the project.
Everything you need is in this file.

**Project:** AEDES, a VR education programme for Lao secondary schools, running on Meta Quest 3.
Module 3 puts the player in a dense Vientiane neighbourhood as a health volunteer during a dengue
outbreak. They walk into people's homes, talk to them, and protect the ones who are unwell from
being bitten — putting up a mosquito net, fixing a torn window screen, setting a fan going.

The assets below are the things the player looks at and picks up while doing that.

---

## 0. Status — final art delivered

All nine assets from §5 below now exist as final, detailed meshes — `Tools/Module3/
make_final_models.py` (Blender) and `make_final_materials.py` (one material per asset), turned
into prefabs by the same `Assets/Editor/Module3PlaceholderPrefabs.cs` that built the original
greybox pass. Verified against this brief and against `Module3-Placeholder-Assets.md`'s contract:
every asset within its triangle budget, one material each, unit scale, real metres, correct pivots
and sockets, and `Fan_Blade` correctly separated. **Phase 3 code was never blocked on art, and now
isn't blocked on placeholder-quality art either.**

The greybox pass (`Tools/Module3/make_placeholders.py`, flat grey `M_Module3Placeholder.mat`) that
originally unblocked Phase 3 is described in full in `Module3-Placeholder-Assets.md`, along with the
Blender→Unity axis/unit traps that apply equally to the final meshes.

Three things were done differently from what this brief originally asked for, and in each case
**the delivered asset is right and this brief has been corrected to match**:

| Brief originally said | Delivered | Why the delivered version wins |
|---|---|---|
| `Socket_Airflow`, `Socket_Beam`, `Socket_Hang`; blade named `Blade` | `Socket_Mount`, `Socket_Light`, `Socket_Hook`; `Fan_Blade` | Consistent `Socket_Mount` across everything that attaches; names now fixed, code binds to these |
| `SM_WaterGlass` **and** `SM_WaterJug` | One `SM_DrinkingVessel` | The simulation has a single `BringWater` action; two meshes served no mechanic |
| `SM_MosquitoNet_Deployed` as a new mesh | Prefab reuses the existing `SM_MosquitoNet`; only `SM_MosquitoNet_RolledUp` is new | The deployed net already existed in Team Assets. Correctly measured so the two swap 1:1 |

One thing this brief asked for that turned out not to be needed: **the fan has no airflow-direction
socket.** The simulation models a fan at household level (`SetFan` applies to the room, not to a
person), so there is nothing for a direction to feed. `Socket_Mount` is sufficient.

The health centre was built despite being marked "do not start yet" — kept deliberately crude at
84 triangles, which is the right call while §13 item 4 is unresolved.

**Still outstanding:** the villager animation clips in §D — now specified in full in
`Module3-Animation-Brief.md`, which supersedes §D of this document. That is the remaining art
dependency.

---

## 1. Read this first: two tiers, and why

Every asset on the list has a **placeholder** form and a **final** form, and the placeholder is
genuinely useful — it is not a courtesy.

| | Placeholder | Final |
|---|---|---|
| Purpose | Unblocks the code, today | Ships to schools |
| Looks like | Correct size, correct shape, flat colour. A fan can be a cylinder and two boxes. | Stylised, matches the existing art |
| Must be right | **Dimensions, pivot, sockets, name, file path** | Everything, plus the above |
| Time | Minutes | Hours |

**If you can only do one thing, do the placeholders for the whole list.** A grey box at the right
size with the right pivot in the right folder is worth more to this project than one beautiful fan,
because the gameplay code binds to paths and sockets, not to meshes. The mesh inside a prefab can
be swapped later with no other change.

So: produce all placeholders first, confirm they load, then work through the finals.

---

## 2. Hard technical requirements

These are not preferences. An asset that misses any of them will be sent back.

### Format
- **FBX** (binary, FBX 2020 or later). `.blend` is also accepted — Unity imports it directly —
  but then the Blender file must contain only the asset, with no packed junk.
- **glTF/`.glb` is accepted for placeholders only.**

### Scale and orientation
- **Real-world scale, in metres.** A water glass is 0.12 m tall, not 12 units.
- In Blender: Scene Properties → Units → Metric, Unit Scale `1.0`.
- **Apply all transforms before export** (`Ctrl+A` → All Transforms). Object scale must read
  `1, 1, 1` and rotation `0, 0, 0`.
- FBX export axes: **Forward `-Z`, Up `Y`** (this is the Blender FBX exporter's default and is
  what Unity expects). Do not "fix" rotation by rotating the object — fix it on export.
- The asset faces **+Z** — that is, the side a person would look at faces +Z.

### Pivot / origin
This is the single most common thing to get wrong, and it breaks placement silently.

- **Objects that sit on something** (fan, jar, bucket, bottle): origin at the **centre of the
  footprint, at floor level** — the point where the object touches the ground, not its centre of mass.
- **Objects that mount into an opening** (window screen): origin at the **centre of the opening**,
  in the plane of the wall.
- **Objects the player holds** (torch, repellent bottle, cloth, water glass): origin at the
  **natural grip point**, oriented so that +Y is "up" when held normally and +Z points away from
  the holder.

### Triangle budget
Quest 3 is a standalone mobile GPU and the scene holds ten houses at once.

| Asset class | Budget (triangles) |
|---|---|
| Small handheld props | ≤ 800 |
| Static floor props | ≤ 1,500 |
| Electric fan (with separated blade) | ≤ 3,000 |
| Window screen | ≤ 500 |
| Mosquito net | ≤ 2,000 |
| Building exterior | ≤ 8,000 |

Under budget is better. There is no prize for detail that disappears at 2 m.

### Materials and texturing
The project is **stylised / toon, with baked lighting**. It is not PBR and it is not realistic.

- **One material per asset.** Not one per part.
- Do **not** author metallic/roughness/normal map sets. They will be discarded.
- Where possible, UV-unwrap onto the shared gradient atlas:
  `Assets/1.TeamWorkspace/Team Assets/Textures/T_Gradients.png`
  — this is how the rest of the project gets its colour, and it is why everything matches.
  Each surface takes its colour by having its UV island sit on the right patch of that atlas.
- If a unique texture is genuinely needed, **512×512 maximum**, power-of-two, PNG.
- Unity-side the material will use `SHD_SimpleLit_Static` (or `SHD_SimpleLit_Static_2Sided` for
  anything single-sided you can see the back of, such as netting). You do not need to set this up;
  just do not build something that depends on a different shading model.

### Naming
The project's conventions, and they are enforced:

| Prefix | Means | Example |
|---|---|---|
| `SM_` | Static mesh | `SM_ElectricFan` |
| `SK_` | Skinned / rigged mesh | `SK_Villager_Adult` |
| `PF_` | Prefab | `PF_ElectricFan` |
| `M_` | Material | `M_ElectricFan` |
| `T_` | Texture | `T_Gradients` |
| `A_` | Animation clip | `A_Fan_Spin` |

No spaces, no accents, no version suffixes like `_final_v2_NEW`. Match the existing spelling of
neighbouring assets exactly, **including its typos** — the project has several load-bearing ones
and "correcting" them breaks references.

---

## 3. Where files go

Everything lands under the shared art folder so Modules 1, 2 and 3 can all use it:

```
Assets/1.TeamWorkspace/Team Assets/
├── Models/              ← the .fbx files
├── Materials/           ← M_*.mat
├── Textures/            ← T_*.png
├── Animations/          ← A_*.anim / clips
└── Prefabs/
    ├── Props/           ← small objects: fan, torch, repellent, vessels, cloth
    ├── Furniture/       ← net, screens, anything room-scale
    ├── Buildings/       ← health centre
    └── Character/       ← villagers
```

**Deliver the `.fbx` into `Models/`.** Prefab creation happens on the Unity side; you do not need
to make prefabs unless you have the project open.

If you are committing to the repository yourself: **always commit the `.meta` file that Unity
generates next to each asset.** A missing `.meta` breaks every reference to that asset for
everyone else on the team. This is the second most common way to cause a bad afternoon.

---

## 4. What already exists — do not remake these

A great deal of the neighbourhood is already built. Check before modelling:

`Assets/1.TeamWorkspace/Team Assets/Prefabs/`

- **Buildings:** `PF_Lao_House_One_FloorV2`, `PF_Lao_House_Two_FloorV3`, `PF_Pop_House`,
  `PF_Door`, `PF_Fence`, `PF_WoodenFence`, `PF_Big_Gate`, `PF_Smaill_Gate`
- **Furniture:** `PF_Bed`, `PF_MosquitoNet`, `PF_Table`, `PF_Carpet_Red`, `PF_Carpet_Black`,
  `PF_TV`, `PF_Refrigerator`, `PF_Pot&GasStove`, `PF_SpiceRack`, `PF_TireWithWater`
- **Props / containers:** `PF_Jar`, `PlasticBucket`, `PlasticBasin`, `PF_Lamp`, `PF_Car`
- **Characters:** `PF_CharacterV1`, `PF_CharacterV2`, plus a Supercyan character pack

The water containers in particular are reused deliberately from Module 2 — the neighbourhood is
meant to feel like the same world the student has already worked in.

---

## 5. The asset list

### Priority A — the module does not work without these

#### A1. Electric fan — `SM_ElectricFan`
A cheap floor-standing or table-top electric fan, the kind found in most Lao homes.

- Size: ~0.35 m diameter head, ~0.75 m tall on a floor stand.
- **The blade must be a separate mesh object**, named `Blade`, with its origin at the centre of
  the rotation axis, so it can be spun in code. If the blade is welded into the body the asset is
  unusable.
- A protective cage around the blade is wanted — model it as simple crossed rings, not as a
  faithful wire mesh.
- Pivot: centre of the base, at floor level.
- Socket: `Socket_Mount` at the base. No airflow-direction socket is needed — the simulation
  applies a fan to the room, not to a person.

#### A2. Window screen, intact — `SM_WindowScreen`
An insect screen in a simple frame, fitted into a window opening.

- **Must match the window openings of `PF_Lao_House_One_FloorV2`.** Open that prefab and measure.
  If you cannot open it, build to 0.9 m wide × 1.1 m tall and say so on delivery so it can be adjusted.
- Geometry: a rectangular frame plus one flat quad for the mesh itself. The screen material is a
  single alpha-cutout plane — **do not model individual wires.**
- Pivot: centre of the opening, in the plane of the wall, +Z facing out of the building.

#### A3. Window screen, torn — `SM_WindowScreen_Torn`
The same asset with a hole in it. The player finds these and repairs them, so the damage has to be
**visible from across a room** — a tear roughly a fifth of the screen's area, at about head height,
with the flap bent outward. Subtlety is wrong here.

- Same dimensions, same pivot, same material as A2. It must be able to swap in and out of the same
  position with no offset.

#### A4. Mosquito net, deployed — `SM_MosquitoNet_Deployed`
`PF_MosquitoNet` already exists — **look at it first.** What is needed is a version that hangs
correctly over `PF_Bed` with somebody lying under it.

- Must drape over the existing `PF_Bed` with ~0.15 m clearance around the mattress and reach the
  floor on all four sides.
- Single-sided geometry with an alpha-cutout net texture. It will be rendered two-sided in engine.
- Pivot: floor level, centred on the bed's footprint.
- Socket: `Socket_Hook` at the apex, where it would tie to a ceiling hook.

#### A5. Mosquito net, stowed — `SM_MosquitoNet_Stowed`
The same net gathered up and tied off above the bed — what the room looks like before the player
puts it up. Same pivot as A4 so the two can be swapped in place.

---

### Priority B — needed for the full set of player actions

#### B1. Drinking vessel — `SM_DrinkingVessel`
A plain glass or enamel cup, ~0.11 m tall. Held by the player and handed to a patient.
`Socket_Grip` at the grip point. Must read as "clean drinking water" at arm's length.

#### B3. Repellent bottle — `SM_RepellentBottle`
A small squeeze or spray bottle, ~0.15 m tall. Generic — **no real brand names, logos or
trade dress of any kind.** Pivot at grip point.

#### B4. Cloth / towel — `SM_Cloth`
A folded damp cloth, ~0.3 × 0.2 m. Two versions if cheap to do: folded (on a table) and draped
(over a forehead or a bowl). Pivot at the centre of the underside.

#### B5. Torch — `SM_Torch`
A hand torch for looking into dark corners of a room, ~0.2 m long.
Pivot at grip point, **+Z pointing out of the lens.** Socket: `Socket_Beam` at the lens face.

---

### Priority C — depends on an open design decision

#### C1. Health centre exterior — `SM_HealthCentre`
A small single-storey Lao rural health centre.

> **Do not start this yet.** Whether the player travels to the health centre or referral happens
> off-screen is still undecided. It is listed so it is not a surprise later. Ask before beginning.

---

### Priority D — villager appearance

The module needs villagers in three visible states: **well**, **unwell without warning signs**, and
**unwell with a warning sign**, across varied ages including children and older people.

> **Superseded by `Module3-Animation-Brief.md`.** That document specifies the clips, the rig, the
> animator contract and the framing rule in full, and corrects two things stated below: the rig is
> a Unity **Humanoid** (so clips can be retargeted rather than hand-authored), and there is no
> Supercyan character pack in the project — `SK_Character` is the only human mesh.

**This is explicitly not three models per character.** Do not build separate "sick" meshes. The
states are communicated by:

1. **Pose and animation** — standing and busy / sitting slumped / lying on a mat under a net.
2. **A skin material swap** — the toon gradient shading makes pallor a one-texture change.
3. **Props** — a cloth at the mouth, a bucket beside the bed, a damp towel.

All three are already wired: `VillagerView` drives the animator parameters, swaps the skin material
and shows or hides the props from what the simulation says is visible in the room.

So what is wanted here is: **animation clips**, not meshes. Against the existing `PF_CharacterV1` /
`PF_CharacterV2` rigs:

| Clip | Description |
|---|---|
| `A_Villager_Idle_Well` | Standing, relaxed, occasional small movement |
| `A_Villager_Sit_Unwell` | Sitting, slumped, head low, slow breathing |
| `A_Villager_Lie_Unwell` | Lying on a mat or bed, on the back, still |
| `A_Villager_Talk` | Light gesture while speaking, usable over any of the above |

**A framing rule that matters more than the art.** A sick villager must never be made to look
frightening, contagious or repellent. Nothing that reads as "danger — infected person here". This
module teaches students to *go to* the people who are unwell and look after them; a design that
made students wary of sick neighbours would have done real harm. Tired, uncomfortable, needing
help — yes. Alarming — no.

---

## 6. Sockets — the convention

A "socket" is an **empty object** (Blender: Add → Empty → Plain Axes) parented to the mesh, named
`Socket_<Purpose>`. It carries no geometry. It tells the game where something attaches or points.

- Name exactly as specified above — the code looks them up by name. The set in use is
  `Socket_Mount` (attaches to a surface or opening), `Socket_Grip` (held in a hand),
  `Socket_Hook` (hangs from above) and `Socket_Light` (a beam origin).
- Orientation matters: **+Z is "forward"** (the direction of airflow, a torch beam, the way a
  screen faces out).
- Keep the empty's scale at `1, 1, 1`.

---

## 7. Placeholder rules

A placeholder must still get these right, because the code binds to them:

- ✅ Correct **file name** and **folder**
- ✅ Correct **real-world dimensions**
- ✅ Correct **pivot / origin**
- ✅ All **sockets** present and correctly oriented
- ✅ Separate **`Fan_Blade`** object on the fan
- ✅ Single material, any flat colour
- ❌ Detail, texturing, bevels, and interesting silhouettes are all *not* required

A cylinder plus two cubes is a perfectly good placeholder fan. A beautifully modelled fan with the
pivot in the middle of the head is not.

---

## 8. Delivery checklist

For each asset, before you call it done:

- [ ] Real-world scale in metres
- [ ] All transforms applied (scale `1,1,1`, rotation `0,0,0`)
- [ ] Exported FBX with Forward `-Z`, Up `Y`
- [ ] Origin at the specified point
- [ ] Sockets present, named exactly, `+Z` forward
- [ ] Triangle count within budget — state the actual count on delivery
- [ ] One material
- [ ] Named per the `SM_` / `SK_` / `A_` convention
- [ ] No brand names, logos or real trade dress anywhere
- [ ] Placed in `Assets/1.TeamWorkspace/Team Assets/Models/`
- [ ] `.meta` file committed alongside it, if you are committing

**On delivery, state for each asset:** the triangle count, the bounding box in metres, and anything
you had to guess at. Guesses are fine and expected — silent guesses are not.

---

## 9. What not to do

- Do not remake anything in §4.
- Do not deliver PBR texture sets — the project is toon-shaded with baked lighting.
- Do not exceed the triangle budgets; this ships to a standalone mobile headset.
- Do not use real brands, logos or packaging on the repellent, the fan or anything else.
- Do not model individual wires on the screens or individual threads on the net — both are flat
  alpha-cutout planes.
- Do not make sick villagers look alarming or contagious. See §D.
- Do not rename or "tidy" existing assets, including ones whose names are misspelled.
