# Module 3 — Implementation Roadmap

Working plan for building *Module 3 — Community response to a dengue outbreak* in
`simple.BeDev.AEDES.UnityVR`.

Source of truth for design: `AEDES_Module3_Game_Design_Rev3.docx` (Rev 3, 13 Aug 2026).
This document does not restate the design; it says **what gets coded, in what order, and why**.

Target branch: `module-3` off `main`.

---

## Status

| Phase | State |
|---|---|
| 0 — Groundwork | **Done.** Worktree, folder tree, asmdefs. Localization ported from `origin/Boy` with the CSV parser rewritten (RFC-4180, so sentences with commas work) and the Lao font switched to a dynamic atlas — it was static with only 64 glyphs baked. |
| 1 — Epidemic model | **Done.** `Module3/Sim/`, headless and deterministic. |
| 1.5 — Calibration harness | **Done.** `Module3/Tests/`, 200 seeds. All five of section 5's guarantees hold. |
| 7a — GAMA parameter delivery | **Done, brought forward.** Parameters now arrive as a GAMA-generated CSV, including mid-session. See the section below. |
| 2 — Session, turns, handover | **Done.** `Sim/Session.cs` + `HandoverBrief`, driven by `M3Session`. `M3DebugDriver` plays a whole session from the keyboard with no headset and no art. |
| 3 — Household visit in VR | Next. Needs the placeholder props from the asset brief. |
| 4–6, 7b, 8 | Not started. |

### Calibration, 200 seeds

| Strategy | Secondary cases | Design requirement |
|---|---|---|
| do nothing | 14.0 | outbreak grows ✅ |
| cleared every container, no nets | 10.5 | still grows ✅ |
| net the well | 13.6 | no better than nothing ✅ |
| net the first two patients, day 1 | 5.4 | outbreak stops ✅ |
| net every visible patient | 4.7 | 69% of residue from invisible sources ✅ |
| net visible + cleared containers | 3.1 | the two modules together ✅ |

Every session presents at least one referral situation; unaided hospitalisations fall
from 2.3 to 0.7 when a squad refers.

### Parameters come from GAMA

They are no longer C# literals. GAMA generates a `key,value` CSV; Unity reads it, validates it
whole, and can take delivery **mid-session** over the existing WebSocket bridge.

- Template and schema: `Assets/Resources/Module3/Module3Parameters.csv` — hand this to the GAMA
  modellers; it carries units, sections, notes and an apply scope per key.
- Receiver: `Module3/Scripts/Module3Parameters.cs`; bridge: `Module3GamaLink.cs`.
- A mid-session change applies **from the next day, never retroactively** — people already
  infected keep the course of illness they were given, or the trace-back replay stops being
  true. Structural keys are held for the next session.
- **The calibration suite is now the acceptance gate for a GAMA parameter file**, not a tuning
  aid. Run it against a new file before that file goes near a classroom.

---

## 0. The one architectural decision everything hangs on

Read §5 of the design doc as a specification, not as flavour. It demands four things that, taken
together, dictate the architecture:

1. A squad that clears every container but nets nobody **must** watch the outbreak grow.
2. A squad that nets the first two patients on day one **must** see the outbreak stop.
3. At the end, the game **replays what would have happened** had patient zero been covered on day one.
4. One chain must begin at a house where nobody was ever visibly ill (asymptomatic source).

(3) is the hard one. A counterfactual replay means the epidemic has to be **re-runnable from a
seed with one input changed**, and the two runs must differ *only* because of that input. That is
impossible if the epidemic lives in `Update()` loops, coroutines, `Random.Range` and physics.

**So: the epidemic is a deterministic, seeded, headless C# model with no UnityEngine dependency.**
Unity renders it and sends it player actions. It never *is* it.

Everything in Phase 1 exists to make that true. If this decision is deferred, the trace-back
replay and the counterfactual ending get retrofitted at the end of the project, badly, or cut —
and they are the two moments the whole module is built to deliver.

Corollary: it is unit-testable and playable-by-script long before there is a scene, a headset or
a single finished 3D model. That is what lets art and code run in parallel.

### The one deviation from CLAUDE.md I'm proposing

CLAUDE.md §2 says "no assembly definitions for game code", because adding one breaks the implicit
cross-folder references in `Assembly-CSharp`. I want one exception:

```
Assets/1.TeamWorkspace/Module3/Sim/AEDES.Module3.Sim.asmdef   (no references at all)
Assets/1.TeamWorkspace/Module3/Tests/AEDES.Module3.Sim.Tests.asmdef  (EditMode, refs the above + NUnit)
```

This is safe precisely because the sim is a leaf: it references nothing, not even `UnityEngine`.
`Assembly-CSharp` auto-references assembly definitions, so every MonoBehaviour in the project can
still use `AEDES.Module3.Sim` types with no further wiring. Nothing existing changes. The payoff
is that the model gets real NUnit tests — the first automated tests in this repo — and the
calibration harness in Phase 1.5 becomes possible.

---

## 1. What already exists (salvage audit)

### From `origin/Boy`, commit `3f40c27 "Start_M3"` (13 Aug 2026)

A greybox, not a foundation. Contents and verdict:

| Asset | Verdict |
|---|---|
| `Assets/M3/Scenes/SampleScene.unity` | ~32 primitive cubes named `House (n)` on a plane, baked NavMesh, world-space canvas with `ToHos` / `ToHome` buttons. **Discard the scene**, keep the idea. |
| `Assets/M3/Scenes/Start.unity` | Title/menu scene with the Lao font. **Keep**, it is the M3 entry point. |
| `HumanM3.cs` (`BodyTemp`, `isSick`, `ToHosPital()`) | Prototype of the villager. Replaced by the Phase 3 view layer, but the *interaction shape* (look at player, raise a world-space panel) is right. |
| `RaomingHuman.cs` | NavMeshAgent random-walk. Useful for street-life NPCs; villagers in M3 are mostly in houses. **Keep as background-life only.** |
| `M3Manager.cs` | 16 lines, a singleton with a hospital transform. **Rewrite.** |
| `Player_Test.cs` | ⚠️ **Has `using UnityEditor.Rendering.LookDev;` in a runtime script.** This compiles in the editor and breaks the Android player build. Must not survive into `module-3`. |
| `SceneTransition.cs` | Clean fade-to-black scene loader, already URP-overlay-camera aware. **Keep verbatim.** |
| `CanvasFollower.cs` | Duplicate of the Module 2 one. **Use the Module 2 copy**, delete this. |
| `TestButton.cs` | Delete. |
| **Localization upgrade** — `LocalizedKey.cs`, `ColorTexts.cs`, `Lao_SomVang SDF_Custom.asset`, Lao column in `LocalizationData.csv` | ⭐ **The most valuable thing on the branch.** Module 3 is text-heavy and must ship in Lao. Cherry-pick this first. |

### From the rest of the repo

Reusable straight away, from `Team Assets/Prefabs/`:

- `Buildings/`: `PF_Lao_House_One_FloorV2`, `PF_Lao_House_Two_FloorV3`, `PF_Door`, `PF_Fence`,
  `PF_WoodenFence`, `PF_Big_Gate`, `PF_Smaill_Gate` — the neighbourhood exterior is largely already built.
- `Furniture/`: **`PF_MosquitoNet`**, `PF_Table`, `PF_Carpet_*`, `PF_TV`, `PF_Refrigerator`,
  `PF_Pot&GasStove`, `PF_TireWithWater` — interiors are largely already dressed.
- `Prefabs/PF_Bed.prefab`, `Props/PF_Jar`, `PlasticBucket`, `PlasticBasin` — the container set from Module 2.
- `Character/PF_CharacterV1`, `PF_CharacterV2`, plus the Supercyan pack in `Imported by Simple/`.

Reusable code patterns:

- Module 2's `UIHand` / `UI_Hand_Button` / `CameraHandFollow` — the hand-attached radial menu is
  exactly the shape Module 3's action verbs need (§4: "help them rest", "put up the net", …).
- Module 2's `M2Manager` — timer + game-over + `DontDestroyOnLoad` singleton, a template for `M3Session`.
- Module 1's `SaveManager` — JSON at `Application.persistentDataPath`, the mechanism for passing
  Module 2's neighbourhood state into Module 3 (open question 3b).
- `SendRecieveData : SimulationManager` — the existing GAMA bridge subclass. See Phase 7.

**Art gap is therefore much smaller than §11 implies.** Genuinely missing: electric fan, window
screen (intact + torn variants), repellent bottle, drinking vessel, cloth, torch, health-centre
exterior, and villager health-state presentation. That is the outsourcing list — see §4 below.

---

## 2. Phases

Each phase ends in something demonstrable. Phases 1–2 need no art and no headset.

### Phase 0 — Groundwork *(small)*

- Branch `module-3` off `main`. Work in a dedicated git worktree (shared checkout).
- Create `Assets/1.TeamWorkspace/Module3/{Sim,Scripts,Scenes,Prefabs,Data,Tests}`.
- Cherry-pick the localization upgrade from `origin/Boy` (`LocalizedKey`, `ColorTexts`, Lao TMP
  font, CSV Lao column) onto `module-3`. Keep it isolated so it can be PR'd to `main` separately —
  Modules 1 and 2 want it too.
- **Fix the CSV parser while doing it.** `LocalizationManager` splits on `,`. Module 3's strings
  are full sentences ("she has been hot for three days"), all of which contain commas. Move to a
  quoted-CSV or TSV parse. This is a hard blocker for every line of dialogue in the module and is
  five minutes of work now versus a week of mangled strings later.
- Port `SceneTransition.cs` and `Start.unity` from `origin/Boy`; leave `Player_Test.cs` behind.
- Add the M3 scenes to `EditorBuildSettings`. Note `SaveManager` wipes the save file when the
  active scene's build index is 0 **or 1** — check where M3 lands in the list before assuming
  anything persists.

**Done when:** an empty M3 scene loads from the Start scene on the headset and a Lao sentence
containing a comma renders correctly.

---

### Phase 1 — The epidemic model *(the core; no Unity)* ⭐

`Assets/1.TeamWorkspace/Module3/Sim/` — pure C#, `System` only, deterministic, seeded.

**Types**

```
Neighbourhood   seed, day, Households[], TransmissionLog, NetBudget
Household       id, plot position, Residents[], Containers[], screens (ok/torn/missing),
                windowsClosedAtDusk, hasFan, mosquitoPopulation, visitLog
Person          id, age band, HealthState, dayInfected, isAsymptomatic,
                protection flags (net / screened room / fan / repellent / covered clothing)
HealthState     Susceptible → Exposed → Infectious(Febrile | Asymptomatic)
                → Warning(bleeding | vomiting) → Recovered | Hospitalised
MosquitoCohort  per household: count, Susceptible / Exposed(EIP) / Infectious
TransmissionEvent  day, sourcePerson, sourceHousehold, mosquitoOriginContainer,
                   targetPerson, targetHousehold, wasPreventable
```

**Rules to implement (all from §5 and §6)**

- Container count → mosquito production per household. Vector density is *per household*, and
  mosquitoes disperse only to **adjacent plots** (Aedes aegypti ~100 m lifetime range, §5). This
  adjacency is what makes "cover your patient and you protect the neighbours" legible.
- Each simulated day: infectious people are bitten with a probability reduced by their protection
  flags; a bite may infect a susceptible mosquito (mosquito enters EIP); infectious mosquitoes bite
  susceptible residents of their own and adjacent households.
- Incubation 4–10 days; EIP 8–12 days. **Do not compress these** — §8 is explicit that the
  compression lives in the calendar (time jumps at handover), not in the biology.
- Asymptomatic share configurable, default 50–75% (§5, pending Marcombe). One asymptomatic source
  seeded such that exactly one end-of-game chain traces to a house with no visible illness.
- Warning-sign onset is weighted toward the *defervescence* window — "danger often arrives as the
  fever comes down" (§8) — so that a Pilot who sees someone improving and moves on is punished by
  the model, not by a script.
- Protection effectiveness ranking must be **data, not code** (`Data/Module3Config.asset` or JSON),
  because §6 says Marcombe/CMPE will re-rank it. Net > screen > closing at dawn/dusk > fan >
  repellent > long sleeves, each a multiplier on bite probability.

**Player actions the model accepts** — and no others:

`PutUpNet(personId)`, `RepairScreen(householdId)`, `SetFan(householdId)`, `AdviseClosingHours(householdId)`,
`GiveRepellent(personId)`, `BringWater(personId)`, `HelpRest(personId)`, `ReferToHealthCentre(personId)`,
`Visit(householdId)`.

There is deliberately **no** `ClearContainer` action (§5: "not a button, not a task, not a score")
and **no** diagnose or treat action (§4, §12).

**Done when:** `Neighbourhood.Run(seed, strategy, days)` produces an identical `TransmissionLog`
on two runs with the same seed, and a differing one when a single action is added.

---

### Phase 1.5 — The calibration harness *(don't skip this)* ⭐

An EditMode test suite + a headless runner that plays scripted strategies over hundreds of seeds
and **asserts the design's guarantees hold**:

| Strategy | Required outcome |
|---|---|
| Do nothing | Outbreak grows |
| Clear every container, net nobody | Outbreak still grows (§5 bullet 1) |
| Net every *well* person | Outbreak still grows — new cases keep appearing |
| Net the first two patients on day one | Outbreak stops, even with containers standing (§5 bullet 2) |
| Net all visible patients | Outbreak nearly stops, **but one chain survives** — the asymptomatic one (§5) |

These are the module's learning objectives expressed as failing tests. If a parameter change
breaks one of them, the module stops teaching what it exists to teach, and CI (or `dev.sh`) says
so immediately. This is also the artefact to hand Dr Marcombe: "here is what the model does with
your numbers."

**Done when:** all five assertions pass over ≥200 seeds, and the harness prints an outbreak curve
per strategy for the clinical review.

---

### Phase 2 — Session, turns and handover *(thin Unity layer; still no art)*

`Module3/Scripts/Session/`

- `M3Session` — `DontDestroyOnLoad` singleton (pattern: `M2Manager`). Owns the `Neighbourhood`,
  the round counter, the net budget, and the seed.
- `TurnTimer` — fixed 2–4 min, **enforced by the game, not the class** (§8). Length comes from
  config, because it is unsettled (§13 item 3). A countdown that is visible on the cast, not only
  to the Pilot (§8).
- `HandoverScreen` — on turn end: freeze, advance the model by *n* days, show "Three days later."
  and the incoming-Pilot brief (who was visited, what was found, what was left undone, §8).
- `RoundController` — 3 turns = 1 round ≈ 1 week; 5–6 rounds = one outbreak of 2–3 weeks.
- `NetBudget` — fewer nets than households (§5). Non-negotiable; it is the whole choice.

**Done when:** a full 5-round session can be played end-to-end with keyboard debug actions in a
grey scene, the outbreak evolves between turns, and the handover screen states the time jump.

**Done.** Two bugs the tests caught, both worth remembering the shape of:

- `UnreferredWarningGraceDays` was 2 against a 3-day handover jump, so a warning sign could appear
  *and* send itself to hospital between two turns. The squad never saw it and could not have acted
  — section 4's referral rule was unreachable rather than merely hard. Any parameter where a
  consequence is shorter than the handover jump has this failure mode; `ParameterCsv` now refuses
  such a file.
- The febrile and defervescing windows overlapped on the last day of fever, so on that day an
  uncovered patient was reported as uncovered and never as "looking better" — masking precisely
  the moment section 8 wants the Coach to warn about.

---

### Phase 3 — The household visit in VR *(art-light, greybox props)*

`Module3/Scripts/Interaction/`

- `HouseholdView` — binds a sim `Household` to scene objects: which residents are present, where
  they are, whether a net/screen/fan is installed. Re-reads state on every turn start so returning
  to a house shows change.
- `VillagerView` — three visible states (§11): well / unwell without warning signs / unwell with a
  warning sign. Driven by **animation state + props**, not by separate meshes (see §4 below).
- `DialogueSystem` — plain-language answers only (§7 step 2): "she has been hot for three days",
  "he has not kept water down since last night". Data-driven from the localization CSV so Lao and
  English come from the same table. Never clinical terms.
- `ActionMenu` — hand-attached radial, reusing Module 2's `UIHand` / `UI_Hand_Button`. Verbs are
  the Phase 1 action list, labelled as §4 requires: *help them rest*, *bring water*, *put up the
  net*, *fix the screen*, *set the fan*, *get them to the health centre*. **Never "diagnose" or
  "treat".** The absence of those buttons is itself a teaching device.
- `ReferralFlow` — referral is not a judgement call (§4). Bleeding or persistent vomiting → the
  referral action is available and correct. An unnecessary referral is *gently corrected, never
  penalised* (§10).
- `MosquitoView` — reuse `Mosquto.cs` from Module 2 (note: file/class name mismatch, don't rename
  it without preserving the `.meta` GUID). Spawn density reads from the sim's per-household
  mosquito count, so a cleared yard visibly differs from an uncleared one (§5).

**Framing rule, enforced in code review:** no mechanic may isolate, mark, or penalise a sick
character; the player is never rewarded for avoiding them (§5). If a UI element ever reads as
"warning: infected person here", it is a bug.

**Done when:** one household can be walked into, talked to, looked at, acted on, and left — and
the sim registers all of it.

---

### Phase 4 — Consequence and evidence *(the payoff)* ⭐

The module's two strongest moments. Both are pure reads off the `TransmissionLog`.

- `TraceBackReplay` — when a new case appears, a short camera path shows the source house, the
  container the mosquito came from, and the night the patient slept unprotected (§5). Every field
  it needs is already in `TransmissionEvent`.
- `OutbreakMap` — end-of-session neighbourhood map showing every chain that ran and every chain
  that was stopped. A squad that netted patient zero on day one sees an almost empty map.
- `CounterfactualReplay` — **before any score is shown** (§5), re-run the model from the same seed
  with `PutUpNet(patientZero)` injected on day 1, and show the map that would have resulted. This
  is the single highest-value feature in the module and it is nearly free, *given Phase 1*.
- `AsymptomaticChain` — one chain on the map originating at a house where nobody was visibly ill,
  as the hook for the Module 2 bridge in the debrief (§5).
- `FieldJournalExport` — the squad's record. Closes with one prompt: "Why did the outbreak stop,
  or why did it not?" Export as text/JSON the facilitator can read aloud.

**Done when:** a played session ends with trace-backs seen during play, then the map, then the
counterfactual, then the score — in that order.

---

### Phase 5 — Squad layer *(low code, high classroom value)*

- The Coach watches via Quest screencast — no code, but the HUD must be legible on a 2D cast:
  countdown, current household, net budget remaining.
- `CoachBrief` — the handover summary screen, readable by someone not in the headset.
- Analyst's Field Journal is paper or a tablet; the game only has to export (Phase 4) and to make
  the return-visit deltas legible ("we saw her on day one and she was only tired").

---

### Phase 6 — Art integration *(runs in parallel from Phase 0)*

See §4 below for the asset plan. Code-side work is only: prefab contract, socket transforms,
greybox → final swap. Nothing in Phases 1–5 may block on an asset.

---

### Phase 7 — GAMA and the Module 2 handoff

§5 says the mosquito population "should be driven by the GAMA model rather than scripted", and
§13 item 3b leaves the Module 2 → Module 3 inheritance unsettled. Both are handled by one seam:

```csharp
interface INeighbourhoodStateSource {
    NeighbourhoodState Load();   // container counts per plot, starting mosquito density
}
```

- `LocalStateSource` — reads JSON from `Application.persistentDataPath` written by Module 2
  (the `SaveManager` pattern). Ships first, works with no network.
- `GamaStateSource` — subclass `SimulationManager` (never edit it, CLAUDE.md §9), override
  `ManageOtherMessages`, take container/population state off the wire.
- `DesignerStateSource` — fixed presets for classroom use when Module 2 wasn't played.

**Known traps before touching this:** `SendRecieveData` reads `GameManager.instance.score`, which
does not exist outside Module 1 — Module 3 will NRE the same way Module 2 does. And two
`SimulationManager` subclasses on one GameObject fight over `Instance` in `Awake`. Put exactly one
on the M3 manager object.

---

### Phase 8 — Build, headset, playtest

- Add M3 scenes to `EditorBuildSettings`, Android/IL2CPP build, on-headset pass.
- Drop in `Assets/Resources/Prefabs/Utils/Debug Overlay` for in-VR logs.
- Playtest to settle turn length (2 vs 3 vs 4 min) and round count (§13 item 3). The calibration
  harness already tells us the *model* works at each length; playtesting decides which one a
  fourteen-year-old can finish a household in.
- Success criterion from the doc, not from a bug tracker: **a class can say the sentence back
  unprompted.** Test it with a class before calling the module done.

---

## 3. Sequencing

```
Phase 0  ──┬─ Phase 1 ── Phase 1.5 ──┬─ Phase 2 ── Phase 3 ──┬─ Phase 4 ── Phase 5 ── Phase 8
           │                         │                       │
           └─ Phase 6 (art, parallel throughout) ────────────┘
                                     └─ Phase 7 (any time after Phase 1)
```

Phases 1 and 1.5 are the critical path and the part that most repays care. Phase 4 is nearly free
once Phase 1 is right, and nearly impossible if it is not.

---

## 4. 3D models — what to make, what to reuse, what to outsource

### Reuse first (already in the repo)

The neighbourhood exterior and most interior dressing exist. `PF_Lao_House_One_FloorV2`,
`PF_Lao_House_Two_FloorV3`, fences, gates, doors, `PF_Bed`, **`PF_MosquitoNet`**, `PF_Table`,
carpets, stove, fridge, TV, plus the whole Module 2 container set (`PF_Jar`, `PF_TireWithWater`,
`PlasticBucket`, `PlasticBasin`). §11 asks for the neighbourhood to "feel like the same world the
student has already worked in" — reuse is the requirement, not a shortcut.

### Villager health states — do this with animation, not models

§11 asks for three visible states across varied ages. **Do not commission three meshes per
character.** Use one character rig per age band and express state through:

- pose and animation (standing / sitting slumped / lying on a mat under a net),
- a material swap on the skin gradient map (the toon style makes pallor a one-texture change),
- and props: a cloth at the mouth, a bucket by the bed, a damp towel.

This is cheaper, reads better in VR at conversational distance, and — importantly — keeps the
framing protective rather than marking sick characters with a visual "flag" (§5).

### The genuine gap — the outsourcing list

| Asset | Notes | Route |
|---|---|---|
| Electric fan (floor/table, blade animates) | Central to §6 — the realistic alternative where there's no screen | **Blender, by hand** — needs a separated, animated blade and a clean pivot |
| Window screen — intact | Must fit the existing Lao house window openings | **Blender, by hand** — dimensions must match `PF_Lao_House_*` |
| Window screen — torn | The variant the player repairs (§6) | Same source file, variant mesh |
| Rolled-up / deployed net variants | `PF_MosquitoNet` exists; need an "up" and "down" state | Modify the existing prefab |
| Repellent bottle | Small, static | **AI-generated or asset store** |
| Drinking vessel / water bottle | Small, static | **AI-generated or asset store** |
| Cloth / towel | Small, static (or a simple cloth sim) | **AI-generated or asset store** |
| Torch | Small, static, needs a light socket | **AI-generated or asset store** |
| Health centre exterior | Only if §13 item 4 resolves to "the player travels there" — **blocked on that decision**, so schedule it last | Blender or asset store |

### Recommendation on AI-generated models

Workable for the small static props (repellent, vessel, cloth, torch) via Meshy/Tripo/Rodin-class
tools. Not workable for the fan (needs an animatable blade on a correct pivot), the screens (must
match existing window geometry to the millimetre), or the net (must drape over our specific
`PF_Bed`). Anything that has to *fit* or *move* should be modelled by hand against the existing
prefab. Anything that just sits on a table can be generated.

Whatever the source, everything goes through the same normalisation pass before it enters the
repo: decimate to budget, re-UV onto the shared `T_Gradients` atlas, assign an `SHD_SimpleLit_*`
material. Otherwise Module 3 will visibly not match Modules 1 and 2.

### Prefab contract (give this to whoever makes the models)

- **Format:** FBX, metres, Y-up / -Z forward, all transforms applied, origin at the floor contact
  point (props: at the natural grip point).
- **Budget:** static props ≤ 1.5k triangles; the fan ≤ 3k; screens ≤ 500.
- **Materials:** exactly one per prop, URP, using `T_Gradients` where possible. No PBR texture sets
  — the project is stylised/toon with baked lighting.
- **Naming:** `SM_` static mesh, `SK_` skinned, `PF_` prefab, `M_` material, `T_` texture,
  `A_` animation (CLAUDE.md §8). Match existing spelling conventions exactly, typos included.
- **Sockets:** empty transforms named `Socket_Grip`, `Socket_Mount` where the object attaches to a
  hand, a bed or a window.
- **Delivery:** drop into `Assets/1.TeamWorkspace/Team Assets/`, commit **with `.meta` files**.

### Unblocking pattern

Commit a **greybox placeholder prefab at the final path and name** for every asset above, on day
one of Phase 0. Code binds to the path and the sockets; art replaces the mesh inside the prefab
later. Nothing in Phases 1–5 ever waits for a model, and the swap is a one-file change with no
scene churn.

---

## 5. Open questions — and why none of them block code

§13 lists six unsettled items. Each maps to a config value rather than a code path:

| §13 | Question | How code stays unblocked |
|---|---|---|
| 1 | Module title ("Code Red: Outbreak" vs "Vientiane Health Leader") | Localization key `module3.title`. Chosen at the last minute. |
| 2 | Marcombe / CMPE clinical review | Every clinical number lives in `Module3Config`. The calibration harness re-validates the design guarantees when they change. |
| 3 | Turn length (2–4 min), round count | Config values; `TurnTimer` reads them. Playtest decides. |
| 3b | Module 2 → Module 3 inheritance | `INeighbourhoodStateSource`, three implementations (Phase 7). |
| 4 | Is the health centre a place you travel to? | **The only one with real build cost** — it decides whether a health-centre interior/exterior and a travel loop are needed. Model the referral as an off-screen outcome first; adding travel later is additive. Chase this answer early. |
| 5 | Lao terminology for warning signs | Localization CSV rows; NUOL fills them in. |

Item 4 is the one worth pushing for an answer on before Phase 3.

---

## 6. Risks and repo traps

1. **Unity YAML merge conflicts.** `Module_1_MainScene.unity` is ~38k lines and two people cannot
   edit a scene at once. Build the M3 neighbourhood from **prefabs placed by a builder script**
   driven by a data asset, not by hand-dragging 10 plots into a scene file. The scene stays small,
   the layout becomes reviewable as data, and merges stop hurting.
2. **`Player_Test.cs` has an editor-only `using` in a runtime script** — will break the Android
   build. Do not carry it over from `origin/Boy`.
3. **`SendRecieveData` reads `GameManager.instance`**, which only exists in Module 1. Module 3 must
   not repeat Module 2's NRE.
4. **Two `SimulationManager` subclasses on one GameObject** fight over `Instance` in `Awake`. One only.
5. **`SaveManager` wipes the save at build index 0 or 1.** Check where the M3 scenes land in
   `EditorBuildSettings` before relying on persistence for the Module 2 handoff.
6. ~~**The localization CSV parser splits on `,`.**~~ Fixed — the parser is RFC-4180 now and
   quoted values may contain commas and line breaks.
7. **`Mosquto.cs` declares `class Mosquito`** — filename/class mismatch. References resolve by
   GUID; renaming the file breaks them unless the `.meta` GUID is preserved.
8. ~~**No CI and no tests in this repo today.**~~ Phase 1.5 added the first ones, under
   `Module3/Tests/`. Still not wired into `dev.sh` — do that, or they will not get run.

9. **A negative day is not an unset day.** An index case was infected before the squad arrived,
   so their exposure, onset and infectious window can all sit before day 0. An early version
   guarded with `InfectiousStartDay >= 0` to mean "has this been scheduled", which silently made
   the outbreak's own starting cases non-infectious — every chain then traced to somewhere else,
   nets did nothing, and a one-day shift in a seeded case swung the outbreak threefold. The model
   now carries an explicit `Person.HasBeenInfected` flag and a regression test. Worth remembering
   the shape of it: in this model, "unset" and "before the session started" look identical.

---

## 7. First session's work

If picking this up cold, in order:

1. Branch `module-3` off `main` in a worktree.
2. Cherry-pick the Boy-branch localization upgrade and **fix the CSV parser**.
3. Create the `Module3/` folder tree and the two asmdefs.
4. Write `Person`, `Household`, `Neighbourhood`, `TransmissionEvent` and the day tick — no Unity.
5. Write the five calibration tests from Phase 1.5 and let them fail.
6. Tune the model until they pass.

That is the module. Everything after it is presentation.
