using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;
using UnityEngine;
using System.IO;
using System.Text;

namespace LGBTQPlusMod {
    class InteractionWorker_RomanceAttempt_Inclusive : InteractionWorker_RomanceAttempt {
        private bool debug = false;
        public InteractionWorker_RomanceAttempt_Inclusive() {
            if(debug)Log.Message("Gayness Loaded!");
        }
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient) {
            if (LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient)) {
                return 0f;
            }
            float num = this.AttractionTo(initiator, recipient);
            if (num < 0.25f) {
                return 0f;
            }
            int num2 = initiator.relations.OpinionOf(recipient);
            if (num2 < 5) {
                return 0f;
            }
            if (recipient.relations.OpinionOf(initiator) < 5) {
                return 0f;
            }
            float num3 = 1f;
            Pawn pawn = LovePartnerRelationUtility.ExistingMostLikedLovePartner(initiator, false);
            if (pawn != null) {
                float value = (float)initiator.relations.OpinionOf(pawn);
                num3 = Mathf.InverseLerp(50f, -50f, value);
            }
            float num5 = Mathf.InverseLerp(0.25f, 1f, num);
            float num6 = Mathf.InverseLerp(5f, 100f, (float)num2);
            return 1.15f * num5 * num6 * num3;
        }
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks) {
            if (Rand.Value < this.SuccessChance(initiator, recipient)) {
                List<Pawn> list;
                this.BreakLoverAndFianceRelations(initiator, out list);
                List<Pawn> list2;
                this.BreakLoverAndFianceRelations(recipient, out list2);
                for (int i = 0; i < list.Count; i++) {
                    this.TryAddCheaterThought(list[i], initiator);
                }
                for (int j = 0; j < list2.Count; j++) {
                    this.TryAddCheaterThought(list2[j], recipient);
                }
                initiator.relations.TryRemoveDirectRelation(PawnRelationDefOf.ExLover, recipient);
                initiator.relations.AddDirectRelation(PawnRelationDefOf.Lover, recipient);
                TaleRecorder.RecordTale(TaleDefOf.BecameLover, new object[]
                {
                    initiator,
                    recipient
                });
                initiator.needs.mood.thoughts.memories.RemoveMemoryThoughtsOfDefWhereOtherPawnIs(ThoughtDefOf.BrokeUpWithMe, recipient);
                recipient.needs.mood.thoughts.memories.RemoveMemoryThoughtsOfDefWhereOtherPawnIs(ThoughtDefOf.BrokeUpWithMe, initiator);
                initiator.needs.mood.thoughts.memories.RemoveMemoryThoughtsOfDefWhereOtherPawnIs(ThoughtDefOf.FailedRomanceAttemptOnMe, recipient);
                initiator.needs.mood.thoughts.memories.RemoveMemoryThoughtsOfDefWhereOtherPawnIs(ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood, recipient);
                recipient.needs.mood.thoughts.memories.RemoveMemoryThoughtsOfDefWhereOtherPawnIs(ThoughtDefOf.FailedRomanceAttemptOnMe, initiator);
                recipient.needs.mood.thoughts.memories.RemoveMemoryThoughtsOfDefWhereOtherPawnIs(ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood, initiator);
                if (initiator.IsColonist || recipient.IsColonist) {
                    this.SendNewLoversLetter(initiator, recipient, list, list2);
                }
                extraSentencePacks.Add(RulePackDefOf.Sentence_RomanceAttemptAccepted);
                LovePartnerRelationUtility.TryToShareBed(initiator, recipient);
            } else {
                initiator.needs.mood.thoughts.memories.TryGainMemoryThought(ThoughtDefOf.RebuffedMyRomanceAttempt, recipient);
                recipient.needs.mood.thoughts.memories.TryGainMemoryThought(ThoughtDefOf.FailedRomanceAttemptOnMe, initiator);
                if (recipient.relations.OpinionOf(initiator) <= 0) {
                    recipient.needs.mood.thoughts.memories.TryGainMemoryThought(ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood, initiator);
                }
                extraSentencePacks.Add(RulePackDefOf.Sentence_RomanceAttemptRejected);
            }
        }
        public new float SuccessChance(Pawn initiator, Pawn recipient) {
            float num = 0.6f;
            num *= this.AttractionTo(recipient, initiator);
            num *= Mathf.InverseLerp(5f, 100f, (float)recipient.relations.OpinionOf(initiator));
            float num2 = 1f;
            Pawn pawn = null;
            if (recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, (Pawn x) => !x.Dead) != null) {
                pawn = recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, null);
                num2 = 0.6f;
            } else if (recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, (Pawn x) => !x.Dead) != null) {
                pawn = recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, null);
                num2 = 0.1f;
            } else if (recipient.GetSpouse() != null && !recipient.GetSpouse().Dead) {
                pawn = recipient.GetSpouse();
                num2 = 0.3f;
            }
            if (pawn != null) {
                num2 *= Mathf.InverseLerp(100f, 0f, (float)recipient.relations.OpinionOf(pawn));
                num2 *= Mathf.Clamp01(1f - this.AttractionTo(recipient, pawn));
            }
            num *= num2;
            return Mathf.Clamp01(num);
        }

        private float AttractionTo(Pawn initiator, Pawn recipient) {
            int sum = 0;
            string[] parts = initiator.Name.ToStringFull.Split('"');
            string name;
            if (parts.Length > 1)
                name = parts[0].Trim() + " " + parts[2].Trim();
            else
                name = initiator.Name.ToStringFull;

            foreach (char i in name) {
                sum += i;
            }
            float kinsey = (float)(sum % 7) / 7;
            
            if (debug) Log.Message(initiator.Name.ToString() + initiator.Name.ToStringFull + " has kinsey " + kinsey);
            if (initiator.def != recipient.def || initiator == recipient) {
                return 0f;
            }
            if (initiator.story.traits.HasTrait(TraitDef.Named("Aromantic/Asexual"))) {
                return 0f;
            }
            float num2 = 1f;
            float ageBiologicalYearsFloat = initiator.ageTracker.AgeBiologicalYearsFloat;
            float ageBiologicalYearsFloat2 = recipient.ageTracker.AgeBiologicalYearsFloat;
            float minBound = 16;
            if (ageBiologicalYearsFloat - 15 > minBound) minBound = ageBiologicalYearsFloat-15;
            num2 = GenMath.FlatHill(minBound, ageBiologicalYearsFloat-5, ageBiologicalYearsFloat+5, ageBiologicalYearsFloat + 15f, ageBiologicalYearsFloat2);
            num2 = 1;
            if(initiator.gender != recipient.gender) {
                kinsey = 1 - kinsey;
            }
            kinsey *= 2;
            kinsey = Mathf.Clamp01(kinsey);
            float num3 = 1f;
            num3 *= Mathf.Lerp(0.2f, 1f, recipient.health.capacities.GetEfficiency(PawnCapacityDefOf.Talking));
            num3 *= Mathf.Lerp(0.2f, 1f, recipient.health.capacities.GetEfficiency(PawnCapacityDefOf.Manipulation));
            num3 *= Mathf.Lerp(0.2f, 1f, recipient.health.capacities.GetEfficiency(PawnCapacityDefOf.Moving));
            float num4 = 1f;
            foreach (PawnRelationDef current in initiator.GetRelations(recipient)) {
                num4 *= current.attractionFactor;
            }
            float num5 = 0;
            if (recipient.RaceProps.Humanlike) {
                num5 = (float)Math.Pow(1.2 ,recipient.story.traits.DegreeOfTrait(TraitDefOf.Beauty));
            }
            float num6 = Mathf.InverseLerp(16f, 18f, ageBiologicalYearsFloat);
            float num7 = Mathf.InverseLerp(16f, 18f, ageBiologicalYearsFloat2);

            return kinsey * num2 * num3 * num4 * num5 * num6 * num7 * 10;
        }
        private void BreakLoverAndFianceRelations(Pawn pawn, out List<Pawn> oldLoversAndFiances) {
            oldLoversAndFiances = new List<Pawn>();
            while (true) {
                Pawn firstDirectRelationPawn = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, null);
                if (firstDirectRelationPawn != null) {
                    pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, firstDirectRelationPawn);
                    pawn.relations.AddDirectRelation(PawnRelationDefOf.ExLover, firstDirectRelationPawn);
                    oldLoversAndFiances.Add(firstDirectRelationPawn);
                } else {
                    Pawn firstDirectRelationPawn2 = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, null);
                    if (firstDirectRelationPawn2 == null) {
                        break;
                    }
                    pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Fiance, firstDirectRelationPawn2);
                    pawn.relations.AddDirectRelation(PawnRelationDefOf.ExLover, firstDirectRelationPawn2);
                    oldLoversAndFiances.Add(firstDirectRelationPawn2);
                }
            }
        }

        private void TryAddCheaterThought(Pawn pawn, Pawn cheater) {
            if (pawn.Dead) {
                return;
            }
            pawn.needs.mood.thoughts.memories.TryGainMemoryThought(ThoughtDefOf.CheatedOnMe, cheater);
        }

        private void SendNewLoversLetter(Pawn initiator, Pawn recipient, List<Pawn> initiatorOldLoversAndFiances, List<Pawn> recipientOldLoversAndFiances) {
            bool flag = false;
            string label;
            LetterType type;
            Pawn t;
            if ((initiator.GetSpouse() != null && !initiator.GetSpouse().Dead) || (recipient.GetSpouse() != null && !recipient.GetSpouse().Dead)) {
                label = "LetterLabelAffair".Translate();
                type = LetterType.BadNonUrgent;
                if (initiator.GetSpouse() != null && !initiator.GetSpouse().Dead) {
                    t = initiator;
                } else {
                    t = recipient;
                }
                flag = true;
            } else {
                label = "LetterLabelNewLovers".Translate();
                type = LetterType.Good;
                t = initiator;
            }
            StringBuilder stringBuilder = new StringBuilder();
            if (flag) {
                if (initiator.GetSpouse() != null) {
                    stringBuilder.AppendLine("LetterAffair".Translate(new object[]
                    {
                        initiator.LabelShort,
                        initiator.GetSpouse().LabelShort,
                        recipient.LabelShort
                    }));
                }
                if (recipient.GetSpouse() != null) {
                    if (stringBuilder.Length != 0) {
                        stringBuilder.AppendLine();
                    }
                    stringBuilder.AppendLine("LetterAffair".Translate(new object[]
                    {
                        recipient.LabelShort,
                        recipient.GetSpouse().LabelShort,
                        initiator.LabelShort
                    }));
                }
            } else {
                stringBuilder.AppendLine("LetterNewLovers".Translate(new object[]
                {
                    initiator.LabelShort,
                    recipient.LabelShort
                }));
            }
            for (int i = 0; i < initiatorOldLoversAndFiances.Count; i++) {
                if (!initiatorOldLoversAndFiances[i].Dead) {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("LetterNoLongerLovers".Translate(new object[]
                    {
                        initiator.LabelShort,
                        initiatorOldLoversAndFiances[i].LabelShort
                    }));
                }
            }
            for (int j = 0; j < recipientOldLoversAndFiances.Count; j++) {
                if (!recipientOldLoversAndFiances[j].Dead) {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("LetterNoLongerLovers".Translate(new object[]
                    {
                        recipient.LabelShort,
                        recipientOldLoversAndFiances[j].LabelShort
                    }));
                }
            }
            Find.LetterStack.ReceiveLetter(label, stringBuilder.ToString().TrimEndNewlines(), type, t, null);
        }
    }
}
