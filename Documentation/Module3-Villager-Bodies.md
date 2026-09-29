# Module 3 — villager bodies

Answers `Module3-Animation-Brief.md` §7 ("Age variety — a question, not a task"). That section
asked which of three options is right for children and elders and said the decision should not
happen by default. **Option 2 was chosen** (new meshes on their own Humanoid skeletons), so this
records what it delivered, what it cost, and the rules the bodies follow.

## What exists

| Prefab | Mesh | Tris | Height | Skeleton |
|---|---|---|---|---|
| `Prefabs/Character/PF_Villager_Elder` | `SK_Villager_Elder` | 1732 | 1.58 m | elder (shorter adult-scale) |
| `Prefabs/Character/PF_Villager_Child_A` | `SK_Villager_Child_A` | 1388 | 1.10 m | child |
| `Prefabs/Character/PF_Villager_Child_B` | `SK_Villager_Child_B` | 1760 | 1.10 m | child (same rig as A) |

For comparison the three existing adult bodies (`fodo`/`nasa`/`Roger`, in `SK_Character.fbx`) are
592–712 tris and one shared material. These are about twice that and use five materials each
(Skin, Hair, Cloth, Trim, and one dark Eye material shared by all three). Budget cap 1800. Paths are under `Assets/1.TeamWorkspace/Team Assets/`;
meshes live at `Models/<name>/<name>.fbx`, materials at `Materials/M_Villager_*` (16 materials, all URP/Lit).

Child B costs almost nothing beyond A because it reuses the child skeleton — the same way the
three adult bodies share one rig. More variants of an age band are a new mesh, not a new rig.

## Matching the art that is already there

`SK_Character` is a strongly super-deformed figure, measured from its bones: 1.69 m tall, **head
about 31% of height**, legs about 35%, shoulders ±0.18 m, A-pose arms ~20–25° below horizontal,
knees ~3 cm forward of the hip–ankle line. The new bodies use the same language (elder ≈ 0.94×
that, children with a head nearer 40% of height). A first pass used realistic proportions and
looked like a different game next to it, so the numbers in `make_villagers.py` are measured, not
guessed. Knees and elbows are bent slightly so Unity's Humanoid mapper gets an unambiguous hinge
direction; a perfectly straight chain leaves it to chance.

## How Lao village context is carried — and what is deliberately not

By dress and setting only:

- **Bare feet.** People go barefoot inside Lao homes and every Module 3 scene is indoors, so this is
  accurate context, not a marker.
- **Elder:** an ankle-length wrapped skirt (sinh) with a contrasting hem band, a waistband, a sash
  across the shoulder (pha biang), long sleeves with cuffs, grey hair in a low bun.
- **Children:** a collared tee with shorts and cropped hair; a tunic and knee-length skirt with
  pigtails and hair ties.
- One plain warm-brown skin tone for everyone.

**Faces carry only what the existing characters already have:** the same two plain vertical eye
bars `SK_Character` has, identical on every villager. No nose, no mouth, no shaped or angled eyes.
(An earlier version of this file said "blank faces"; that was wrong — the existing characters have
the eye bars, and children without them stood out beside an adult with them.) All variety is
clothing, hair and colour. Do not add facial features or vary head or body shape to signal
ethnicity: that is where caricature starts. The same framing rule as the rest of Module 3 applies:
the unwell skin is the same tone made paler and a little greyer — tired, not diseased — and nothing
here may read as "danger, infected person" (Animation Brief §2).

## Rig and animation

- **Humanoid, configured by script.** `Assets/Editor/Module3VillagerPrefabs.cs` sets Animation Type
  to Humanoid and the 17-bone map through `ModelImporter.humanDescription`, which is what
  *Configure Avatar → Apply* does, instead of hand-writing ~150 lines of YAML. The bone names
  need not match `SK_Character`'s (`pelis`, `spine1`, …); retargeting goes through the avatar.
  The `.fbx.meta` files created by `make_villager_unity_assets.py` start Generic and the script
  fixes them, so run the tools in order.
- **Facing.** `SK_Character` faces Blender −Y, which is Unity +Z after the exporter's own
  Z-up→Y-up conversion; villagers are aimed with `LookRotation`, so +Z must be forward. Everything
  here faces −Y, and `Verify` measures the baked mesh to check it. These use Blender's *ordinary*
  FBX axes, not the prop pipeline's custom no-conversion export (`make_final_models.py`), which
  exists to solve a problem — parented `Socket_*` empties — that a skinned character does not have.
- **They reuse `NpcAnimationController`** — and stand in the same idle pose as the existing adults (arms
  down at the sides), which was measured in the running game, not assumed. Its idle is a Humanoid clip inside `SK_Character.fbx`, so
  it retargets. Note that clip is *a static pose plus a small root sway*, not limb animation, so
  villagers currently stand in their A-pose bind, same as the existing ones. To prove real
  retargeting `Verify` plays the shared walk clip: the foot swings 52 cm on the elder and 32 cm on
  the children, scaled to each body. Any clip built later for `AC_Villager_M3` retargets to all of
  them with no extra work — Animation Brief §7's promise, checked.
- **Skinning is rigid** (one bone per part, weight 1). Predictable and fine for idle-class poses
  with small joint rotations. The skirt cone is bound to the hips, so a future *seated* clip would
  leave it standing round the pelvis with the thighs coming out of the front (an A-line drape) —
  acceptable, and nothing pulls it through the legs.
- **Skin is material slot 0** on purpose. `VillagerView` swaps well/unwell skin through the singular
  `skinRenderer.sharedMaterial`, which always addresses slot 0. The prefabs come pre-wired with
  `wellSkin`/`unwellSkin` — for the existing bodies those fields are never populated.

## Runtime wiring

- `M3NeighbourhoodBuilder` has `childPrefabs[]` and `elderPrefabs[]` and picks by age band and
  person id. Purpose-built bodies are not scaled by age (that was the hack); only the fallback
  adult body still is, so a scene without the new arrays behaves as before.
- **The adult prefab was drawing three bodies at once.** `PF_CharacterV1` carries `fodo`, `nasa` and
  `Roger` as active siblings, each 1.6–1.7 m tall with a different head, and nothing chooses one.
  `ShowOneBody` keeps one per person (from the id, so stable across rebuilds), which also makes the
  three existing looks actually appear as different people.
- `VillagerView.bodyHeight` (default 1.7, the adult the offsets were measured on) scales the
  lying-down offsets, so a child is not laid half a metre off the foot of the bed.
- The prefabs carry a `CapsuleCollider` sized to the body; the builder's fallback capsule is adult-sized.
- `Module_3_MainScene.unity` gains five lines (the two arrays). `Module3SceneBuilder` assigns them
  too, so regenerating the scene keeps them.

## Regenerating

```bash
blender -b --factory-startup --python Tools/Module3/make_villagers.py     # meshes; fails on budget or facing
python3 Tools/Module3/make_villager_unity_assets.py                        # .meta files, stable GUIDs
python3 Tools/Module3/make_villager_materials.py                           # 15 flat-colour materials
# Unity:  AEDES ▸ Module 3 ▸ Build Villager Prefabs   then   Verify Villager Contract
Unity -batchmode -nographics -quit -projectPath . -executeMethod Module3VillagerPrefabs.Build -logFile /tmp/v.log
```

As with every Unity batchmode run here, read the `[Module3]` block in the log, not the exit code.

## Traps found while building this

- **`SHD_SimpleLit_Static` ignores `_BaseColor`.** It takes its colour from a gradient texture, so a flat
  colour on it renders solid black in the game while looking correct in Blender and in the inspector.
  The first version of these materials did exactly that; only playing the scene showed it. They are
  URP/Lit now, like the rest of Team Assets and like `M_Module3Placeholder.mat` after its own fix.
- Blender's `join` orders material slots by first appearance, so parts are appended Skin → Hair →
  Cloth → Trim → Eye to keep Skin at slot 0.
- `GetComponent<T>() ?? AddComponent<T>()` is wrong in the editor: Unity overloads `== null`, and a
  missing built-in component comes back as a placeholder that `??` treats as present. Use `== null`.
- Hair caps are cut by UV-sphere latitude; a threshold on a ring is off by one ring. The hair is an
  open shell (no lid inside the head), with a higher front hairline than back.

## Checked in the running game

With the playtest harness (a throwaway extract of `Assets/Playtest`, not committed) the scene was played
and shot from 2.2 m at eye height: colours, scale against the house, facing, the retargeted idle, and
a child and an elder laid on beds with `bodyHeight` (heads at the pillow end, inside the mattress).
Two things only showed up this way and both were fixed: the black materials (above), and the eye bars.
One caveat for anyone repeating it: Unity's animator culling leaves villagers the game camera cannot
see in bind pose (arms out) until they come into view; set `cullingMode` to AlwaysAnimate for shots.

## Not done

- Only one elder and two children. Adults still use the existing three bodies.
- No lying or seated clips exist; that remains the Animation Brief's job. Lying is the standing pose
  tipped flat (`VillagerView`), so an A-pose idle body lies with its arms slightly out.
- Not seen on a headset. Proportions, colours and the toon shading at conversational distance in VR
  are worth a look before calling them final.
