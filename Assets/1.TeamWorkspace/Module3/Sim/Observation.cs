using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// How a person appears to a volunteer standing in the room. Not a diagnosis.
    ///
    /// Section 4: "The player never names the illness, never prescribes anything and never gives
    /// medicine. They observe, they arrange care, and they refer." So there is no "infected" here,
    /// no "dengue", and no state the player could not have seen with their own eyes. A person
    /// incubating the virus, or carrying it with no symptoms at all, looks exactly like a person
    /// who is well - because they do.
    /// </summary>
    public enum VisibleCondition
    {
        /// <summary>Nothing to see. Includes people incubating and people who never feel ill.</summary>
        Seems_Well,

        /// <summary>Feverish, aching, tired. Most people with dengue. Cared for at home.</summary>
        Unwell,

        /// <summary>Bleeding, or cannot keep anything down. Goes to a doctor now. Section 4.</summary>
        NeedsADoctorNow,

        /// <summary>
        /// The fever has broken. Section 8: "feeling better is not the all-clear. The dangerous
        /// phase often begins as the fever drops."
        /// </summary>
        Improving,

        AtTheHealthCentre,
    }

    /// <summary>Something the player can see in the room, as a localization key.</summary>
    public struct VisibleSign
    {
        public string Key;
        public static VisibleSign Of(string key) => new VisibleSign { Key = key };
    }

    public sealed class PersonObservation
    {
        public int PersonId;
        public int HouseholdId;
        public AgeBand Age;
        public VisibleCondition Condition;

        /// <summary>
        /// What the household says, in days: "she has been hot for three days". Section 7 wants
        /// plain language, not clinical terms, so this is what the family has noticed rather than
        /// anything the model knows.
        /// </summary>
        public int DaysUnwell;

        /// <summary>The line the household gives, and the number to substitute into it.</summary>
        public string DialogueKey;
        public int DialogueDays;

        /// <summary>Things in the room. Section 7 step 3: "what is visible".</summary>
        public readonly List<VisibleSign> Signs = new List<VisibleSign>();

        public bool HasNet;
        public bool HasRested;
        public bool HasFluids;
        public bool AlreadyReferred;

        public bool IsUnwell => Condition == VisibleCondition.Unwell
                             || Condition == VisibleCondition.NeedsADoctorNow;
    }

    public sealed class HouseholdObservation
    {
        public int HouseholdId;
        public ScreenState Screens;
        public bool HasFan;
        public bool HasAirConditioning;
        public bool ClosesDuringBitingHours;

        /// <summary>
        /// Containers in the yard. Present as scenery and as the visible reason there are
        /// mosquitoes here - section 5 is explicit that clearing them is not an action in this
        /// module, so this is something to notice and be told about, never something to do.
        /// </summary>
        public int VisibleContainers;

        /// <summary>Roughly how many mosquitoes are about. What a cleared yard looks like versus one that is not.</summary>
        public int MosquitoesAbout;

        public int LastVisitedDay;
        public bool NeverVisited;

        public readonly List<PersonObservation> Residents = new List<PersonObservation>();

        public bool AnyoneUnwell
        {
            get
            {
                for (int i = 0; i < Residents.Count; i++) if (Residents[i].IsUnwell) return true;
                return false;
            }
        }
    }

    /// <summary>One verb offered to the player, with why it is or is not available.</summary>
    public struct AvailableAction
    {
        public ActionKind Kind;
        public int PersonId;
        public int HouseholdId;
        /// <summary>Localization key for the button. Never "diagnose", never "treat" - section 4.</summary>
        public string LabelKey;
        public bool Enabled;
        /// <summary>If disabled, why - shown as a quiet note rather than a refusal.</summary>
        public string DisabledReasonKey;

        public PlayerAction ToAction() => new PlayerAction
        {
            Kind = Kind,
            PersonId = PersonId,
            HouseholdId = HouseholdId,
        };
    }

    /// <summary>
    /// Turns model state into what the player sees and what they are offered.
    ///
    /// Keeping this out of the MonoBehaviours is what lets the module's two hardest rules be
    /// tested rather than reviewed: that the player is never offered a clinical action, and that
    /// nothing marks a sick person as a hazard.
    /// </summary>
    public static class Observe
    {
        public static HouseholdObservation Household(Neighbourhood n, int householdId)
        {
            var h = n.HouseholdById(householdId);
            if (h == null) return null;

            int mosquitoes = 0;
            for (int i = 0; i < n.Mosquitoes.Count; i++)
            {
                if (n.Mosquitoes[i].Alive && n.Mosquitoes[i].HouseholdId == householdId) mosquitoes++;
            }

            var obs = new HouseholdObservation
            {
                HouseholdId = h.Id,
                Screens = h.Screens,
                HasFan = h.HasFan,
                HasAirConditioning = h.HasAirConditioning,
                ClosesDuringBitingHours = h.ClosesDuringBitingHours,
                VisibleContainers = h.Containers.Count,
                MosquitoesAbout = mosquitoes,
                LastVisitedDay = h.LastVisitedDay,
                NeverVisited = h.VisitCount == 0,
            };

            for (int i = 0; i < h.ResidentIds.Count; i++)
            {
                obs.Residents.Add(Person(n, h.ResidentIds[i]));
            }
            return obs;
        }

        public static PersonObservation Person(Neighbourhood n, int personId)
        {
            var p = n.PersonById(personId);
            if (p == null) return null;
            int day = n.Day;

            var obs = new PersonObservation
            {
                PersonId = p.Id,
                HouseholdId = p.HouseholdId,
                Age = p.Age,
                HasNet = p.HasNet,
                HasRested = p.HasRested,
                HasFluids = p.HasFluids,
                AlreadyReferred = p.Referred,
            };

            if (p.State == HealthState.Hospitalised)
            {
                obs.Condition = VisibleCondition.AtTheHealthCentre;
                obs.DialogueKey = "m3.say.atHealthCentre";
                return obs;
            }

            if (p.HasVisibleWarningSign(day))
            {
                obs.Condition = VisibleCondition.NeedsADoctorNow;
                obs.DaysUnwell = DaysSinceOnset(p, day);
                obs.DialogueDays = obs.DaysUnwell;

                if (p.Warning == WarningSign.Bleeding)
                {
                    // Plain words. A fourteen-year-old has to be able to act on this without
                    // interpreting it - section 4.
                    obs.DialogueKey = "m3.say.bleeding";
                    obs.Signs.Add(VisibleSign.Of("m3.see.bloodOnCloth"));
                }
                else
                {
                    obs.DialogueKey = "m3.say.cannotKeepWaterDown";
                    obs.Signs.Add(VisibleSign.Of("m3.see.bucketByTheBed"));
                }

                AddRoomSigns(p, obs);
                return obs;
            }

            if (p.IsFebrile(day))
            {
                obs.Condition = VisibleCondition.Unwell;
                obs.DaysUnwell = DaysSinceOnset(p, day);
                obs.DialogueDays = obs.DaysUnwell;
                obs.DialogueKey = obs.DaysUnwell <= 1 ? "m3.say.hotSinceYesterday" : "m3.say.hotForDays";
                obs.Signs.Add(VisibleSign.Of("m3.see.restingOnTheBed"));
                AddRoomSigns(p, obs);
                return obs;
            }

            if (p.IsDefervescing(day))
            {
                // The household will say they are better. They may well not be.
                obs.Condition = VisibleCondition.Improving;
                obs.DaysUnwell = DaysSinceOnset(p, day);
                obs.DialogueDays = obs.DaysUnwell;
                obs.DialogueKey = "m3.say.feelingBetterToday";
                AddRoomSigns(p, obs);
                return obs;
            }

            // Everyone else looks well - including people incubating, and the people who will
            // never feel ill at all. Section 5: the squad cannot act on what it cannot see, and
            // a mechanic the player cannot act on is only frustration.
            obs.Condition = VisibleCondition.Seems_Well;
            obs.DialogueKey = "m3.say.everyoneFineHere";
            return obs;
        }

        private static int DaysSinceOnset(Person p, int day)
        {
            int d = day - p.SymptomOnsetDay;
            return d < 0 ? 0 : d;
        }

        private static void AddRoomSigns(Person p, PersonObservation obs)
        {
            if (p.HasNet) obs.Signs.Add(VisibleSign.Of("m3.see.netIsUp"));
            if (!p.HasFluids) obs.Signs.Add(VisibleSign.Of("m3.see.noWaterWithinReach"));
        }

        // -----------------------------------------------------------------------------------

        /// <summary>
        /// The verbs offered for a person. Section 4: "the action buttons are things like 'help
        /// them rest', 'bring water', 'put up the net', 'get them to the health centre' - never
        /// 'diagnose' or 'treat'."
        /// </summary>
        public static List<AvailableAction> ActionsFor(Neighbourhood n, int personId)
        {
            var list = new List<AvailableAction>();
            var p = n.PersonById(personId);
            if (p == null || p.State == HealthState.Hospitalised) return list;

            var obs = Person(n, personId);

            list.Add(new AvailableAction
            {
                Kind = ActionKind.PutUpNet,
                PersonId = personId,
                HouseholdId = p.HouseholdId,
                LabelKey = "m3.do.putUpTheNet",
                Enabled = !p.HasNet && n.NetsRemaining > 0,
                DisabledReasonKey = p.HasNet ? "m3.why.netAlreadyUp"
                                   : n.NetsRemaining <= 0 ? "m3.why.noNetsLeft" : null,
            });

            if (p.HasNet)
            {
                list.Add(new AvailableAction
                {
                    Kind = ActionKind.ReclaimNet,
                    PersonId = personId,
                    HouseholdId = p.HouseholdId,
                    LabelKey = "m3.do.takeTheNetOn",
                    Enabled = true,
                });
            }

            list.Add(new AvailableAction
            {
                Kind = ActionKind.BringWater,
                PersonId = personId,
                HouseholdId = p.HouseholdId,
                LabelKey = "m3.do.bringWater",
                Enabled = !p.HasFluids,
                DisabledReasonKey = p.HasFluids ? "m3.why.hasWater" : null,
            });

            list.Add(new AvailableAction
            {
                Kind = ActionKind.HelpRest,
                PersonId = personId,
                HouseholdId = p.HouseholdId,
                LabelKey = "m3.do.helpThemRest",
                Enabled = !p.HasRested,
                DisabledReasonKey = p.HasRested ? "m3.why.alreadyResting" : null,
            });

            list.Add(new AvailableAction
            {
                Kind = ActionKind.GiveRepellent,
                PersonId = personId,
                HouseholdId = p.HouseholdId,
                LabelKey = "m3.do.repellentOnSkin",
                Enabled = p.RepellentUntilDay < n.Day,
                DisabledReasonKey = p.RepellentUntilDay >= n.Day ? "m3.why.repellentOn" : null,
            });

            // Always offered, never pre-judged. Section 10: an unnecessary referral is "gently
            // corrected, not punished" AFTER the fact - hiding the button would teach hesitation,
            // and section 4 is explicit that the player "should not weigh it up".
            list.Add(new AvailableAction
            {
                Kind = ActionKind.ReferToHealthCentre,
                PersonId = personId,
                HouseholdId = p.HouseholdId,
                LabelKey = "m3.do.getThemToTheHealthCentre",
                Enabled = !p.Referred,
                DisabledReasonKey = p.Referred ? "m3.why.alreadyGoing" : null,
            });

            return list;
        }

        /// <summary>Verbs offered for the house itself rather than a person.</summary>
        public static List<AvailableAction> ActionsFor(Neighbourhood n, int householdId, bool household)
        {
            var list = new List<AvailableAction>();
            var h = n.HouseholdById(householdId);
            if (h == null) return list;

            list.Add(new AvailableAction
            {
                Kind = ActionKind.RepairScreen,
                HouseholdId = householdId,
                PersonId = -1,
                LabelKey = h.Screens == ScreenState.Missing ? "m3.do.fitAScreen" : "m3.do.mendTheScreen",
                Enabled = h.Screens != ScreenState.Intact,
                DisabledReasonKey = h.Screens == ScreenState.Intact ? "m3.why.screensAreFine" : null,
            });

            list.Add(new AvailableAction
            {
                Kind = ActionKind.SetFan,
                HouseholdId = householdId,
                PersonId = -1,
                LabelKey = "m3.do.setTheFanGoing",
                Enabled = !h.HasFan,
                DisabledReasonKey = h.HasFan ? "m3.why.fanAlreadyOn" : null,
            });

            list.Add(new AvailableAction
            {
                Kind = ActionKind.AdviseClosingHours,
                HouseholdId = householdId,
                PersonId = -1,
                LabelKey = "m3.do.shutUpAtDawnAndDusk",
                Enabled = !h.ClosesDuringBitingHours,
                DisabledReasonKey = h.ClosesDuringBitingHours ? "m3.why.alreadyClosing" : null,
            });

            return list;
        }

        /// <summary>
        /// Every verb the module will ever offer. There is deliberately no ClearContainer
        /// (section 5) and no Diagnose or Treat (sections 4 and 12) - a test asserts it.
        /// </summary>
        public static readonly ActionKind[] OfferedVerbs =
        {
            ActionKind.Visit,
            ActionKind.PutUpNet,
            ActionKind.ReclaimNet,
            ActionKind.RepairScreen,
            ActionKind.SetFan,
            ActionKind.AdviseClosingHours,
            ActionKind.GiveRepellent,
            ActionKind.BringWater,
            ActionKind.HelpRest,
            ActionKind.ReferToHealthCentre,
        };
    }
}
