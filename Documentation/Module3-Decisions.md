# Module 3 — Decisions taken in code

Design decisions the implementation had to make that the design document
(`AEDES_Module3_Game_Design_Rev3.docx`) does not settle. Each is **in force now** on the
recommended option; none is blocking. They are listed here so they can be confirmed, overturned,
or carried into Dr Marcombe's clinical review and the CMPE co-creation workshop rather than
surviving only in commit messages.

Items marked **clinical** should go in front of Dr Marcombe. Items marked **pedagogical** are for
the programme/teaching side. Items marked **technical** need no external sign-off, but the team
should know they were made.

---

### D1 — The simulation is a separate, engine-free assembly · *technical*

`Assets/1.TeamWorkspace/Module3/Sim/` has its own `.asmdef`, which CLAUDE.md §2 otherwise
forbids for game code.

**Why:** the assembly references nothing, not even `UnityEngine`, so it cannot break the implicit
cross-folder references the rule exists to protect. `Assembly-CSharp` auto-references assembly
definitions, so every MonoBehaviour can still use these types. It is what makes the model unit
testable — the first automated tests in this repository.

---

### D2 — The counterfactual covers the first *symptomatic* index case · *pedagogical*

§5 asks the game to replay "what would have happened had the first patient been covered on day
one". The scenario seeds more than one index case, including an asymptomatic one.

**In force:** patient zero for the replay is the first index case who was *visibly* ill.

**Why:** replaying "you could have covered them" only teaches something if the squad could have
found them. Holding a class to account for a person with no symptoms would teach the opposite of
§5's framing, which is emphatic that nobody is at fault for an infection they cannot feel.

---

### D3 — There is an opening brief as well as handover briefs · *pedagogical*

**In force:** the first Pilot gets a brief describing the situation the squad arrives into.

**Why:** the index cases are not "new" — nothing happened during a turn to produce them — so they
never appear in a handover brief. Without an opening brief the starting patients would go
unannounced and the first turn would begin blind.

---

### D4 — Warning-sign cases are guaranteed per session, not left to the rate · *clinical* · *pedagogical*

**In force:** `guaranteed_warning_sign_cases = 2`. The background rate
(`warning_sign_share = 0.08`) still governs everybody else.

**Why:** §4 calls the referral rule "the heart of the module" and §10 calls acting on it "the
strongest positive". At the honest background rate, a large share of sessions would present no
warning sign at all and would simply fail to teach it. This separates clinical realism in the
model from a teaching guarantee in the scenario.

**For review:** confirm this is an acceptable way to present it — students meet the situation
every session, which is far more often than a real volunteer would.

---

### D5 — "Cleared everything" means 80%, with cryptic sites that survive any sweep · *clinical*

**In force:** `Module2Outcome.EverythingCleared` sets clearance to 0.80, and every plot keeps at
least 2 productive containers (`min_productive_containers_per_household`).

**Why:** at 100% clearance there are no mosquitoes and no outbreak, which contradicts §5's
requirement that a squad who cleared every container "should still watch the outbreak grow". A
blocked gutter or water under a slab survives any sweep, so perfect prevention is not achievable
and the module should not imply it is.

**For review:** confirm the residual is realistic for Vientiane households.

---

### D6 — A net can be carried on to the next patient · *pedagogical*

**In force:** a `ReclaimNet` action exists. Taking a net back is always a deliberate choice, never
automatic.

**Why:** §5 requires fewer nets than households so the player must choose. Without reclamation the
budget is exhausted in the first two turns and later patients are unreachable regardless of skill.
With it, the shortage is about where the squad's attention goes across three weeks, which is the
more interesting version of the same constraint.

---

### D7 — A consequence must outlast the handover jump · *pedagogical*

**In force:** `unreferred_warning_grace_days` (4) must exceed `days_per_handover` (3). A parameter
file that breaks this is refused.

**Why:** at 2 days against a 3-day jump, a warning sign could appear *and* send itself to hospital
entirely between two turns. The squad never saw it and could not have acted — the referral rule
became unreachable rather than merely hard. This was a real bug, caught by the session tests.

---

### D8 — The referral button is always offered, never hidden · *pedagogical*

**In force:** "get them to the health centre" is available for any person, whatever their state.

**Why:** §4 says referral "is not a judgement call and the player should not weigh it up". Hiding
the button until it is correct would teach hesitation and would do the discriminating *for* the
student. §10 handles the wrong call afterwards: gently corrected, never punished.

---

### D9 — The counterfactual can occasionally show a worse outcome · *pedagogical* · **needs a call**

**In force:** in roughly 1.5% of neighbourhoods, covering patient zero on day one leaves the
outbreak no smaller — mosquitoes find somebody else. A test bounds this at 5%.

**Why it is left in:** it is honest. Outbreaks are stochastic and suppressing it would mean
fabricating the replay.

**The risk:** in about one class in sixty, the game's closing moment tells a squad that the right
action would not have helped. The facilitator needs an answer for that, or the debrief needs to
frame the replay as "one way it could have gone" rather than "what would have happened".

**This is the one item here that wants a decision from the programme side rather than a note.**

---

### D10 — Mid-session parameters apply from the next day, never retroactively · *technical*

**In force:** a parameter set arriving from GAMA mid-session takes effect on the following
simulated day. People already infected keep the course of illness they were given. Structural
values (plot count, what Module 2 left behind) are held until a new session.

**Why:** rewriting an already-scheduled illness would make the trace-back replay a lie about what
the squad actually saw, and rebuilding the neighbourhood would do it underneath a standing player.

---

### D11 — Timings are compressed in the calendar only · *clinical*

**In force:** incubation 4–10 days and extrinsic incubation 8–12 days are used unmodified. A test
fails if either moves outside those bounds. Population rates (bite probability, emergence,
lifespan) are tuned to make an outbreak legible inside a 50-day session and are labelled as
tuning knobs, not clinical claims.

**Why:** §8 — "The compression is in the calendar, not in the biology", which is what lets the
module tell CMPE that the timings a student learns remain true.

---

### D12 — Socket names follow the delivered assets, not the original brief · *technical*

**In force:** `Socket_Mount`, `Socket_Grip`, `Socket_Hook`, `Socket_Light`, and `Fan_Blade` for the
fan's moving part. The asset brief originally specified `Socket_Airflow`, `Socket_Beam`,
`Socket_Hang` and `Blade`.

**Why:** the placeholder assets were built and verified before any code bound to a socket name, so
the artefact is the source of truth and the brief was corrected. `Socket_Mount` is also more
consistent — one name for everything that attaches to a surface or an opening.

Related: the fan needs no airflow-direction socket. The simulation applies `SetFan` to a household,
not to a person, so there is nothing for a direction to feed.

---

## Still open, from §13 of the design document

| # | Question | Status |
|---|---|---|
| 1 | Module title | Localization key `module3.title`; decide late |
| 2 | Marcombe review, then CMPE approval | Outstanding — the items above marked *clinical* belong in it |
| 3 | Turn length and round count | Config values; playtest decides |
| 3b | How Module 2's state reaches Module 3 | Solved in principle: `module2_container_clearance` in the GAMA parameter CSV |
| 4 | Is the health centre a place the player travels to? | **Still the only open question with real build cost.** Modelled off-screen for now; adding travel later is additive |
| 5 | Lao terminology for the warning signs | Localization rows; NUOL to fill in |
