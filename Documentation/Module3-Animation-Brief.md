# Module 3 — Villager Animation Brief

**For:** whoever is producing the animation — a generative model, an animator, or someone working
in Blender. You do not need to know Unity or the rest of the project. Everything you need is here.

**This follows on from** `Module3-Asset-Brief.md`, whose nine placeholder props are delivered and
merged. This is the remaining art dependency: the villagers themselves.

---

## 1. What this is for

AEDES Module 3 is a VR module for Lao secondary schools. The player is a health volunteer walking
into people's homes during a dengue outbreak. They talk to the household, look around the room, and
protect whoever is unwell from being bitten.

So the player spends the module **standing in a room, two metres from a person, looking at them and
deciding how they are doing.** That is the whole reason this animation matters. There is no combat,
no traversal, no spectacle. There is a person on a bed and a fourteen-year-old trying to work out
whether they are all right.

---

## 2. The rule that outranks everything else in this brief

A sick villager must **never** be made to look frightening, contagious, or repellent.

The module exists to teach students to *go to* the people who are unwell and look after them. Its
design document is explicit: no mechanic may isolate, mark or penalise a sick character, and the
player is never rewarded for avoiding them — they are rewarded for going to them. A version of this
module that left students wary of sick neighbours would have done real harm.

In animation terms:

| Yes | No |
|---|---|
| Tired, heavy, uncomfortable, slow | Twitching, convulsing, rasping, clawing |
| Wants to be still | Lurching, staggering, reaching toward the player |
| A person having a bad few days | A horror-game infected |
| Withdrawn, quiet | Aggressive, or pitiable to the point of being upsetting |

Aim for **"my mum with a bad fever"**, not "a patient". If a clip would look at home in a zombie
game, it is wrong, however well made.

---

## 3. The rig — read this before you start

**File:** `Assets/1.TeamWorkspace/Team Assets/Models/SK_Character/SK_Character.fbx`

**It is a Unity Humanoid rig** (`animationType: 3`, avatar configured). That is the single most
useful fact in this brief, and it means:

- **You can retarget.** Any humanoid animation — Mixamo, an existing library, a generative
  animation tool that outputs humanoid FBX — can be retargeted onto this character by Unity. You do
  not have to hand-key from scratch.
- **You do not have to match the bone names.** Unity maps them through the avatar. (For reference
  the skeleton is Blender-authored with `.L`/`.R` suffixes, IK bones, and at least one misspelling —
  `pelis` for the pelvis. Do not "fix" it; the project has several load-bearing typos and renaming
  breaks references.)
- **Deliver clips as Humanoid**, not Generic. A Generic clip will not retarget and will be sent back.

**What already exists:**

- `Armature_walk.anim` and the controller `NpcAnimationController.controller` (one `isWalk` bool),
  both sitting beside the rig. **Do not modify either** — Module 1's street NPCs use them. Module 3
  gets its own controller (§6).
- The rig FBX itself carries takes named `idle`, `walk` and a t-pose. No clip ranges are configured
  on it, so Unity exposes those takes as-is. The `idle` take is a reasonable thing to build C1 from
  rather than keying from nothing.
- **Three body meshes share this one skeleton** — `fodo`, `nasa` and `Roger`, at 592 / 712 / 656
  triangles, 18 vertex groups each. The neighbourhood already has three villager bodies, and one
  set of clips drives all of them. This matters for §7.
- **There is a humanoid character pack in the project you may retarget from:**
  `Assets/1.TeamWorkspace/Imported by Simple/Supercyan Character Pack Free Sample/`. Its mesh and
  all **12** of its animation FBXs are Humanoid, so they retarget onto `SK_Character` directly —
  `common_people@idle`, `@walk`, `@pickup` and `@wave` are the ones worth looking at. It is
  **read-only**: the project rule is that nothing under `Imported by Simple/` gets edited, but
  retargeting *from* it is fine.

---

## 4. The clips

Five clips. All are **idle-class**: the character stays where it is.

### C1 — `A_Villager_Idle_Well`
A person who is fine, at home, mildly occupied. Standing or sitting, weight shifting occasionally,
a look around the room, maybe folding something. Should survive being looped for a minute without
becoming obviously cyclic.

- **Loops:** yes, seamlessly.
- **Length:** 6–12 s.
- **Note:** this is the *baseline*. Most people in the neighbourhood are well, including people who
  are infected and will never feel a thing — the player is not supposed to be able to tell. Do not
  add "subtly off" cues. Well is well.

### C2 — `A_Villager_Sit_Unwell`
Sitting on the edge of a bed or a mat, fever, three days in. Shoulders down, head heavy, slow
breathing, occasional small shift of weight because nothing is comfortable. Eyes mostly down.

- **Loops:** yes.
- **Length:** 8–14 s. Slow.
- **Note:** the tell is *weight* and *stillness*, not tremor. They are tired, not fitting.

### C3 — `A_Villager_Lie_Unwell`
Lying on their back on a bed or a floor mat. Almost still. Breathing visible in the chest. Perhaps
one slow turn of the head across the loop.

- **Loops:** yes.
- **Length:** 10–20 s. Very slow.
- **Note:** this one plays **under a mosquito net**, which is the module's single most important
  action. The net is a dome measuring **1.56 × 1.77 m in plan and 1.39 m tall**, over a bed of
  **1.60 × 1.94 m**, both centred on the same point. Keep the lying silhouette inside roughly a
  0.9 m width — arms in, nothing flung out — so it reads through the netting and never clips it.
  Load `Team Assets/Prefabs/Props/PF_MosquitoNet_Deployed.prefab` with the bed
  (`Team Assets/Prefabs/PF_Bed.prefab`) and check against the real props.

### C4 — `A_Villager_Talk`
A light speaking gesture — a hand, a small nod, head turning slightly toward the listener. This is
an **additive / upper-body overlay**: it plays *on top of* C1, C2 or C3, so the character can talk
while standing, while sitting hunched, or while lying down.

- **Loops:** yes.
- **Length:** 3–6 s.
- **Deliver two variants if cheap:** `A_Villager_Talk` (upright) and `A_Villager_Talk_Lying`
  (reduced, mostly head). If you only do one, do the upright one.
- **Note:** no mouth shapes needed — there is no facial rig and no lip sync.

### C5 — `A_Villager_Sit_Improving`
The same person as C2, a few days later: the fever has broken. Sitting up, **noticeably better than
C2** — straighter back, head up, present in the room, able to hold a conversation — and still not
well. Tired around the edges. Someone you could believe was on the mend.

- **Loops:** yes.
- **Length:** 8–12 s.
- **Note:** read *How much the body should carry*, immediately below, before animating this one. It
  is the hardest clip in the brief and the only one where the body is the sole honest signal, so it
  deserves more of your time than C2 and C3 together. If it ends up indistinguishable from C2 the
  state may as well not exist; if it reads as fully well, the module misleads instead of teaching.

### How much the body should carry

Four clips cover five things the player can see, because most of what distinguishes those five is
carried by **what the household says** and **what is lying in the room** — not by the body. The
code that decides what a player observes is already written, and it splits the work like this:

| What the player is seeing | What tells them | What the body does |
|---|---|---|
| Someone well | nothing at all | C1 |
| Fever, a few days in | they say *"she has been hot for three days"*, and there is bedding | C2 or C3, tired and heavy |
| **Needs a doctor now** | they say *"there is blood"* or *"she cannot keep water down"*, **and** there is a cloth or a bucket in the room | **the same C2 or C3 — do not escalate** |
| **The fever has broken** | they say *"he is feeling better today"* — **and nothing else** | **C5**, sitting, visibly better than C2 |

Two consequences, and they are the difference between a clip that teaches and one that does not.

**Do not put severity into the performance.** The warning signs already have two signals: a plain
sentence and a prop. If the body escalates on top of that, students learn to scan a room for the
most dramatic person instead of learning the two signs they are meant to carry out of the module —
and a quiet person with a bucket beside the bed gets walked past. **The person who needs a doctor
should not look more alarming than the person with a fever.** They may well look calmer. That is
not a mistake in the brief.

**The fever breaking is the one place where the body is the only honest signal.** When someone is
past the fever, the household says they are better, and there is no prop in the room to say
otherwise. In the real illness this is often exactly when the dangerous phase begins — which is why
the module is built to make a student who sees someone improving and moves on regret it. That makes
this pose the hardest ask in the brief, because it has to be genuinely ambiguous: **better than the
fever pose — more upright, more present, someone you could plausibly leave alone — but not well.**
If they read as fine, the student is being tricked rather than taught. If they read as still
obviously ill, there is nothing to learn. Aim for the version of *"no, no, I'm all right"* that
nobody in the room quite believes.

---

## 5. Technical contract

- **Format:** FBX, one clip per file, or one FBX containing all clips as takes — either is fine.
- **Rig type:** **Humanoid.** Set Animation Type to Humanoid on import; deliver against
  `SK_Character` or any standard humanoid skeleton that retargets to it.
- **Root motion:** **none.** These are in-place idles. Bake root motion out; the character must not
  drift. A clip that translates will walk villagers through walls.
- **Frame rate:** 30 fps. Do not deliver 24 or 60.
- **Looping:** C1–C4 all loop. First and last pose must match; check Unity's loop-match indicator
  reads green, or state on delivery that it does not and why.
- **Naming:** `A_` prefix, exactly as in §4. The project's convention is `SM_` static mesh,
  `SK_` skinned, `PF_` prefab, `M_` material, `T_` texture, `A_` animation.
- **Location:** `Assets/1.TeamWorkspace/Team Assets/Animations/A_Villager/`
- **Commit the `.meta` file** next to every asset. A missing `.meta` breaks every reference to that
  asset for everyone else on the team.

---

## 6. The animator controller

Create **a new controller** — do not extend `NpcAnimationController`, which Module 1 uses.

**File:** `Assets/1.TeamWorkspace/Team Assets/Animations/A_Villager/AC_Villager_M3.controller`

The gameplay code already drives exactly three parameters. These names are fixed — they are hashed
in `VillagerView.cs` and the code is already written and tested against them:

| Parameter | Type | Meaning |
|---|---|---|
| `Unwell` | bool | The person is visibly ill — fever, or a warning sign |
| `Resting` | bool | The person is lying down or sat down because of it |
| `Talking` | bool | The household is speaking to the player right now |

Required behaviour:

| `Unwell` | `Resting` | Plays |
|---|---|---|
| false | false | `A_Villager_Idle_Well` |
| true | false | `A_Villager_Sit_Unwell` |
| true | true | `A_Villager_Lie_Unwell` |
| false | true | **`A_Villager_Sit_Improving`** — the fever has broken, and they are still resting |

Note what the two bools deliberately do **and do not** separate. They tell someone who is improving
apart from someone with a fever, because those must look different. They do **not** tell someone who
needs a doctor apart from someone with a fever, because those must look the *same* — the warning
sign is carried by the line and the prop, and escalating the body would teach the wrong lesson. That
collapse is the design, not a gap in the parameters.

A villager who has gone to the health centre needs no clip. The game hides their body while they are
away, so whatever the controller is playing goes unseen.

`Talking` layers on top of whichever of those is playing, as an additive or upper-body-masked layer.

Transitions should be **slow** — 0.3–0.5 s. A villager snapping between poses at a handover would
undercut the whole thing. Nothing here needs a blend tree.

---

## 7. Age variety — a question, not a task

The simulation gives every villager an age band: **Child, Adult or Elder**, and the design document
asks for "ages varied, including children and older people".

The variety problem is already half solved: three body meshes share the skeleton (§3). But all
three are adult-proportioned, so the gap is specifically **a child and an elder**, not bodies in
general.

**Do not model new characters for this yet.** Tell us which of these you think is right:

1. Scale and proportion variation on the three existing meshes (cheap, but a scaled adult reads as
   a small adult, not as a child).
2. Two more meshes — a child and an elder — on the same Humanoid skeleton, so all four clips
   retarget to all five bodies with no extra animation work.
3. Age carried by clothing and material on the existing three, with body shape unchanged.

Option 2 is probably right, but it is a modelling job with a cost, and it should be a decision
rather than something that happens by default. Say what you would do and roughly what it costs.

Either way, **age does not need its own animation**: one set of clips retargets to every body. If
playtesting later says an elder needs a different weight of movement, that is a follow-up variant,
not part of this delivery.

---

## 8. How to check your own work

Before delivering, in Unity:

1. Drop `PF_CharacterV1` in a scene, assign `AC_Villager_M3`.
2. Tick `Unwell` and `Resting` on and off in the Animator window and watch the transitions. Check
   all four combinations, including `Unwell` off with `Resting` on — that is C5, and it is the one
   most easily left wired to the wrong clip.
3. Add `PF_Bed` and `PF_MosquitoNet_Deployed` around the lying pose. Confirm no clipping through the
   net and that the person is readable through it.
4. Watch `A_Villager_Idle_Well` loop for sixty seconds. If you can see the loop point, it needs work.
5. Stand a camera at eye height, two metres away, and look. That is the actual viewing condition.

**On delivery, state:** clip lengths, whether each loop matches cleanly, what you retargeted from if
anything, and anything you had to guess at. Guesses are expected — silent guesses are not.

---

## 9. What not to do

- Do not modify `NpcAnimationController` or `Armature_walk.anim` — Module 1 depends on both.
- Do not deliver Generic clips. Humanoid only.
- Do not bake in root motion.
- Do not add facial animation or lip sync; there is no facial rig.
- Do not rename existing bones, including the misspelled ones.
- Do not make illness look frightening. See §2 — it outranks everything else here.
