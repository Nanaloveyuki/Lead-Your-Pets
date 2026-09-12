using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    public static class LeadYourPetUtility
    {
        public const string HarmonyId = "lezhizhong.leadyourpet";
        private const string MouseDisasterPackageId = "lezhizhong.mouse.disaster.famine";
        private const string MouseDisasterChildPawnKindDefName = "MouseDisaster_BeggarRatkinChild";
        private const string MouseDisasterTraderPawnKindDefName = "MouseDisaster_TraderRatkinAdult";
        private static readonly Type MouseDisasterUtilityType = AccessTools.TypeByName("MouseDisaster.MouseDisasterUtility");
        private static readonly MethodInfo MouseDisasterGenerateFactionRatkinPawnMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "GenerateFactionRatkinPawn", typeof(PawnKindDef), typeof(Faction), typeof(DevelopmentalStage), typeof(float));
        private static readonly MethodInfo MouseDisasterSetBiologicalAgeYearsMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "SetBiologicalAgeYears", typeof(Pawn), typeof(float));
        private static readonly MethodInfo MouseDisasterPrepareTradablePrisonerMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "PrepareTradablePrisoner", typeof(Pawn), typeof(Faction));
        private static readonly MethodInfo MouseDisasterClearTradeLeaderStateMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "ClearTradeLeaderState", typeof(Pawn));
        private static readonly MethodInfo MouseDisasterResetBeggarStateMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "ResetBeggarState", typeof(Pawn));
        private static readonly MethodInfo MouseDisasterMakeFactionHostileToPlayerMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "MakeFactionHostileToPlayer", typeof(Faction), typeof(bool));
        private static readonly MethodInfo MouseDisasterTryRefreshHiddenFactionRelationsMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "TryRefreshHiddenFactionRelations");
        private static readonly MethodInfo MouseDisasterNotifyIdentityChangedMethod = OptionalModApi.Resolve(MouseDisasterUtilityType, "NotifyMouseDisasterPawnIdentityOrLifeStageChanged", typeof(Pawn));
        private static readonly Type MouseDisasterVisitorControlType = AccessTools.TypeByName("MouseDisaster.MouseDisasterVisitorUtility") ?? AccessTools.TypeByName("MouseDisaster.GameComponent_MouseDisasterVisitorControl");
        private static readonly MethodInfo MouseDisasterTryMakeHostileVisitorsMethod = OptionalModApi.Resolve(MouseDisasterVisitorControlType, "TryMakeHostile", typeof(IEnumerable<Pawn>), typeof(int).MakeByRefType());
        private static readonly Type RimTalkPawnUtilType = AccessTools.TypeByName("RimTalk.Util.PawnUtil");
        private static readonly Type RimTalkCacheType = AccessTools.TypeByName("RimTalk.Data.Cache");
        private static readonly MethodInfo RimTalkIsTalkEligibleMethod = RimTalkPawnUtilType == null ? null : AccessTools.Method(RimTalkPawnUtilType, "IsTalkEligible", new[] { typeof(Pawn) });
        private static readonly MethodInfo RimTalkGetPlayerMethod = RimTalkCacheType == null ? null : AccessTools.Method(RimTalkCacheType, "GetPlayer");

        public static readonly List<LeadYourPetInteractionKind> AutomaticPositiveInteractions = new List<LeadYourPetInteractionKind>
        {
            LeadYourPetInteractionKind.WatchWork,
            LeadYourPetInteractionKind.Fed,
            LeadYourPetInteractionKind.RestAtFeet,
            LeadYourPetInteractionKind.IdleNearOwner,
            LeadYourPetInteractionKind.StudyFloor,
            LeadYourPetInteractionKind.SillySmile,
            LeadYourPetInteractionKind.TailPetting,
            LeadYourPetInteractionKind.EarPetting,
            LeadYourPetInteractionKind.OwnerWaited,
            LeadYourPetInteractionKind.Snack
        };

        public static readonly List<LeadYourPetInteractionKind> BidirectionalInteractions = new List<LeadYourPetInteractionKind>
        {
            LeadYourPetInteractionKind.Kick,
            LeadYourPetInteractionKind.TapHead,
            LeadYourPetInteractionKind.PullTail,
            LeadYourPetInteractionKind.PullEar,
            LeadYourPetInteractionKind.Whirl,
            LeadYourPetInteractionKind.Slap,
            LeadYourPetInteractionKind.Pat,
            LeadYourPetInteractionKind.KickButt
        };

        public static readonly List<LeadYourPetInteractionKind> UnidirectionalInteractions = new List<LeadYourPetInteractionKind>
        {
            LeadYourPetInteractionKind.Observe,
            LeadYourPetInteractionKind.WatchWork,
            LeadYourPetInteractionKind.Fed,
            LeadYourPetInteractionKind.RestAtFeet,
            LeadYourPetInteractionKind.IdleNearOwner,
            LeadYourPetInteractionKind.StudyFloor,
            LeadYourPetInteractionKind.SillySmile,
            LeadYourPetInteractionKind.TailPetting,
            LeadYourPetInteractionKind.EarPetting,
            LeadYourPetInteractionKind.OwnerWaited,
            LeadYourPetInteractionKind.Snack
        };

        public static LeadYourPetGameComponent Component => Current.Game?.GetComponent<LeadYourPetGameComponent>();
        public static int MaxLeashLength => LeadYourPetMod.Settings == null ? 10 : (LeadYourPetMod.Settings.infiniteLeash ? 999 : LeadYourPetMod.Settings.maxLeashLength);
        public static float HardLeashLength => MaxLeashLength >= 999 ? 999f : MaxLeashLength * 1.5f;
        public static float FollowThreshold => Mathf.Max(2f, MaxLeashLength - 2f);
        public static int MaxMouseEggPetLeashStartDistance => LeadYourPetMod.Settings == null ? 10 : Mathf.Max(1, LeadYourPetMod.Settings.maxMouseEggPetLeashStartDistance);

        public static bool CanPlayerCommand(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && pawn.Faction == Faction.OfPlayer
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Awake();
        }

        public static bool IsEligibleAnimalPet(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && pawn.Faction == Faction.OfPlayer
                && pawn.RaceProps.Animal
                && !pawn.Dead
                && !pawn.Downed;
        }

        public static bool IsRatkinHumanlike(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            bool isRatkin = string.Equals(pawn.def.defName, "Ratkin", StringComparison.OrdinalIgnoreCase);
            if (!isRatkin && pawn.kindDef != null && pawn.kindDef.race != null)
            {
                isRatkin = string.Equals(pawn.kindDef.race.defName, "Ratkin", StringComparison.OrdinalIgnoreCase);
            }

            return isRatkin;
        }

        public static bool IsMouseEgg(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return LeadYourPetRules.ShouldTreatAsMouseEgg(
                IsRatkinHumanlike(pawn),
                pawn.DevelopmentalStage.Baby(),
                pawn.DevelopmentalStage.Child(),
                HasMeaningfulMouseEggState(pawn),
                IsMouseDisasterTravelPawn(pawn));
        }

        public static bool IsColonistMouseEgg(Pawn pawn)
        {
            return pawn != null && pawn.IsColonist;
        }

        public static bool IsProtectedLeashedMouseEgg(Pawn pawn)
        {
            if (pawn == null || Component == null)
            {
                return false;
            }

            bool isMouseEgg = IsMouseEgg(pawn);
            bool isBaby = pawn.DevelopmentalStage.Baby();
            bool isChild = pawn.DevelopmentalStage.Child();
            if (!isMouseEgg || (!isBaby && !isChild))
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            MouseEggState state = Component.GetMouseEggState(pawn);
            return LeadYourPetRules.ShouldTreatAsProtectedLeashedMouseEgg(
                isMouseEgg,
                isBaby,
                isChild,
                link != null && (link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby),
                state != null && (state.IsPet || state.CurrentMaster != null));
        }

        public static bool HasMeaningfulMouseEggState(Pawn pawn)
        {
            if (pawn == null || Component == null)
            {
                return false;
            }

            MouseEggState state = Component.GetMouseEggState(pawn);
            return state != null && (state.IsPet || state.CurrentMaster != null || state.IsTravelStock);
        }

        public static bool CanBePetMouseEgg(Pawn pawn, out string reasonKey)
        {
            reasonKey = null;
            if (!IsRatkinHumanlike(pawn))
            {
                reasonKey = "LeadYourPet_Reason_NotRatkinMouseEgg";
                return false;
            }

            if (pawn.Dead)
            {
                reasonKey = "LeadYourPet_Reason_Dead";
                return false;
            }

            if (!pawn.DevelopmentalStage.Baby() && !pawn.DevelopmentalStage.Child())
            {
                reasonKey = "LeadYourPet_Reason_NotBaby";
                return false;
            }

            return true;
        }

        public static bool CanStartPlayerPetLeash(Pawn master, Pawn pet, bool markAsMouseEggPet, out string reasonKey)
        {
            reasonKey = null;
            if (master == null || pet == null)
            {
                reasonKey = "LeadYourPet_Reason_InvalidTarget";
                return false;
            }

            if (markAsMouseEggPet && !CanBePetMouseEgg(pet, out reasonKey))
            {
                return false;
            }

            if (markAsMouseEggPet && !CanStartMouseEggPetLeashAtDistance(master, pet))
            {
                reasonKey = "LeadYourPet_Reason_MouseEggTooFarToLeash";
                return false;
            }

            if (Component != null)
            {
                LeashLink existing = Component.GetLinkForPet(pet);
                if (LeadYourPetRules.ShouldRejectPlayerTakeoverOfNonPlayerLeash(
                    hasExistingLink: existing != null && existing.Master != null,
                    existingMasterIsDifferent: existing != null && existing.Master != master,
                    existingMasterIsPlayer: existing?.Master?.Faction == Faction.OfPlayer,
                    newMasterIsPlayer: master.Faction == Faction.OfPlayer))
                {
                    reasonKey = "LeadYourPet_Reason_LeashedByNonPlayer";
                    return false;
                }
            }

            return true;
        }

        public static bool CanStartMouseEggPetLeashAtDistance(Pawn master, Pawn pet)
        {
            if (master == null || pet == null || master.Map == null || pet.Map == null)
            {
                return false;
            }

            return LeadYourPetRules.CanStartMouseEggPetLeashAtDistance(
                sameMap: master.Map == pet.Map,
                distance: master.Position.DistanceTo(pet.Position),
                maxDistance: MaxMouseEggPetLeashStartDistance);
        }

        public static bool CanStartRatkinMotherLeash(Pawn mother, Pawn baby, out string reasonKey)
        {
            reasonKey = null;
            bool motherIsRatkinHumanlike = IsRatkinHumanlike(mother);
            bool babyIsRatkinHumanlike = IsRatkinHumanlike(baby);
            bool motherIsDead = mother?.Dead ?? true;
            bool babyIsDead = baby?.Dead ?? true;
            bool babyIsBabyStage = baby != null && baby.DevelopmentalStage.Baby();
            bool motherIsBabyStage = mother != null && mother.DevelopmentalStage.Baby();
            bool hasParentRelation = HasParentRelation(baby, mother);

            if (!LeadYourPetRules.CanFormOrMaintainRatkinMotherLeash(
                motherIsRatkinHumanlike,
                babyIsRatkinHumanlike,
                motherIsDead,
                babyIsDead,
                babyIsBabyStage,
                motherIsBabyStage,
                hasParentRelation))
            {
                if (!motherIsRatkinHumanlike || !babyIsRatkinHumanlike)
                {
                    reasonKey = "LeadYourPet_Reason_NotRatkinMouseEgg";
                    return false;
                }

                if (babyIsDead || motherIsDead)
                {
                    reasonKey = "LeadYourPet_Reason_Dead";
                    return false;
                }

                if (!babyIsBabyStage)
                {
                    reasonKey = "LeadYourPet_Reason_NotBaby";
                    return false;
                }

                if (motherIsBabyStage)
                {
                    reasonKey = "LeadYourPet_Reason_InvalidPetStatus";
                    return false;
                }

                if (!hasParentRelation)
                {
                    reasonKey = "LeadYourPet_Reason_NotMotherChild";
                    return false;
                }
            }

            if (Component != null)
            {
                LeashLink existing = Component.GetLinkForPet(baby);
                if (LeadYourPetRules.ShouldRejectNonPlayerLeashReplacement(
                    hasExistingLink: existing != null && existing.Master != null,
                    existingMasterIsDifferent: existing != null && existing.Master != mother,
                    existingMasterIsPlayer: existing?.Master?.Faction == Faction.OfPlayer))
                {
                    reasonKey = "LeadYourPet_Reason_LeashedByNonPlayer";
                    return false;
                }
            }

            return true;
        }

        public static bool ShouldBlockCarryOfProtectedMouseEgg(Pawn carrier, Pawn target)
        {
            return LeadYourPetRules.ShouldBlockProtectedCarry(
                IsProtectedLeashedMouseEgg(target),
                allowColonistCarry: false);
        }

        public static bool ShouldAllowProtectedMouseEggPrisonerTransfer(Pawn carrier, Pawn target)
        {
            Job job = carrier?.CurJob;
            return LeadYourPetRules.ShouldAllowProtectedPawnPrisonerTransfer(
                IsProtectedLeashedMouseEgg(target),
                jobMakesTargetPrisoner: job?.def?.makeTargetPrisoner == true,
                jobTargetMatchesPawn: job?.targetA.Thing == target);
        }

        public static bool ShouldBlockExternalToddlerPickup(Pawn target, string jobDefName)
        {
            return target != null && LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                IsProtectedLeashedMouseEgg(target),
                jobDefName);
        }

        public static bool ShouldEndExternalToddlerHoldJob(Pawn jobPawn, Pawn protectedPawn)
        {
            if (jobPawn == null || protectedPawn == null)
            {
                return false;
            }

            Job curJob = jobPawn.CurJob;
            if (jobPawn != protectedPawn
                && curJob?.targetA.Thing != protectedPawn
                && curJob?.targetB.Thing != protectedPawn
                && curJob?.targetC.Thing != protectedPawn)
            {
                return false;
            }

            return LeadYourPetRules.ShouldEndExternalToddlerHoldJob(
                IsProtectedLeashedMouseEgg(protectedPawn),
                jobPawn.jobs?.curDriver?.GetType().FullName);
        }

        public static bool ShouldBlockExternalDialogue(Pawn target, string integrationName)
        {
            return target != null && LeadYourPetRules.ShouldBlockExternalDialogue(
                IsProtectedLeashedMouseEgg(target),
                integrationName);
        }

        public static bool ShouldTreatMouseEggAsRimTalkEligible(Pawn pawn, bool originalEligible)
        {
            return LeadYourPetRules.ShouldTreatMouseEggAsRimTalkEligible(
                originalEligible,
                IsMouseEgg(pawn),
                IsRimTalkConversationPawnValid(pawn));
        }

        public static bool ShouldAllowRimTalkMouseEggConversation(Pawn initiator, Pawn target, bool originalAllowed)
        {
            return LeadYourPetRules.ShouldAllowRimTalkMouseEggConversation(
                originalAllowed,
                IsMouseEgg(target),
                IsRimTalkConversationPawnValid(initiator),
                IsRimTalkConversationPawnValid(target),
                IsRimTalkTalkEligible(initiator),
                IsRimTalkPlayerPawn(initiator),
                CanReachForRimTalkConversation(initiator, target));
        }

        public static bool ShouldOverrideBabyCryMoodForMouseEgg(Pawn source, Pawn hearer)
        {
            return LeadYourPetRules.ShouldOverrideBabyCryMoodForMouseEgg(
                IsMouseEgg(source),
                IsMouseEggPetState(source),
                IsMouseEgg(hearer),
                hearer?.needs?.mood?.thoughts?.memories != null);
        }

        public static bool ShouldApplyMouseEggCryingNearbyMood(Pawn source, Pawn hearer)
        {
            return LeadYourPetRules.ShouldOverrideBabyCryMoodForMouseEgg(
                IsMouseEgg(source),
                IsMouseEggPetState(source),
                IsMouseEgg(hearer),
                hearer?.needs?.mood?.thoughts?.memories != null);
        }

        public static bool IsMouseEggPetState(Pawn pawn)
        {
            MouseEggState state = Component?.GetMouseEggState(pawn);
            return state != null && state.IsPet;
        }

        private static bool IsRimTalkConversationPawnValid(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead;
        }

        private static bool IsRimTalkTalkEligible(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (RimTalkIsTalkEligibleMethod == null)
            {
                return pawn.RaceProps?.Humanlike == true && IsRimTalkConversationPawnValid(pawn);
            }

            try
            {
                return RimTalkIsTalkEligibleMethod.Invoke(null, new object[] { pawn }) is bool result && result;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRimTalkPlayerPawn(Pawn pawn)
        {
            if (pawn == null || RimTalkGetPlayerMethod == null)
            {
                return false;
            }

            try
            {
                return RimTalkGetPlayerMethod.Invoke(null, null) is Pawn player && player == pawn;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanReachForRimTalkConversation(Pawn initiator, Pawn target)
        {
            if (initiator == null || target == null)
            {
                return false;
            }

            return initiator.CanReach(target, PathEndMode.Touch, Danger.Some);
        }

        public static bool HasParentRelation(Pawn child, Pawn parent)
        {
            if (child == null || parent == null || child.relations == null)
            {
                return false;
            }

            if (child.GetMother() == parent || child.GetFather() == parent)
            {
                return true;
            }

            return child.relations.DirectRelations != null &&
                   child.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Parent && r.otherPawn == parent);
        }

        private static bool IsFriendlyVisitorOrTraveler(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Faction == null || pawn.Faction == Faction.OfPlayer || pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            Lord lord = pawn.GetLord();
            return lord != null && (lord.LordJob is LordJob_TradeWithColony || lord.LordJob is LordJob_VisitColony);
        }

        public static bool IsNonHostileAdultVisitor(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            if (!pawn.DevelopmentalStage.Adult())
            {
                return false;
            }

            return IsFriendlyVisitorOrTraveler(pawn);
        }

        public static bool IsFriendlyVisitorMouseEgg(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !IsMouseEgg(pawn))
            {
                return false;
            }

            return IsFriendlyVisitorOrTraveler(pawn) || (pawn.GetLord()?.LordJob is LordJob_TradeWithColony || pawn.GetLord()?.LordJob is LordJob_VisitColony);
        }

        public static bool IsSuppressedAgainstMasterFaction(Pawn pet, Faction faction)
        {
            if (pet == null || faction == null || Component == null)
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pet);
            return link != null && link.Master != null && link.Master.Faction == faction;
        }

        public static bool IsSuppressedAgainstThing(Thing a, Thing b)
        {
            if (a == null || b == null || Component == null)
            {
                return false;
            }

            Pawn pawnA = a as Pawn;
            Pawn pawnB = b as Pawn;

            if (pawnA != null)
            {
                LeashLink linkA = Component.GetLinkForPet(pawnA);
                if (linkA != null && linkA.Master != null && (b == linkA.Master || b.Faction == linkA.Master.Faction))
                {
                    return true;
                }
            }

            if (pawnB != null)
            {
                LeashLink linkB = Component.GetLinkForPet(pawnB);
                if (linkB != null && linkB.Master != null && (a == linkB.Master || a.Faction == linkB.Master.Faction))
                {
                    return true;
                }
            }

            return false;
        }

        public static Pawn GenerateMouseEggPawn(Faction faction, Map map)
        {
            PawnKindDef kind = ResolveTravelMouseEggPawnKind();
            if (kind == null)
            {
                return null;
            }

            Pawn mouseDisasterPawn = TryGenerateMouseDisasterTravelPawn(kind, faction);
            if (mouseDisasterPawn != null)
            {
                return mouseDisasterPawn;
            }

            float randomAge = kind.defName == MouseDisasterChildPawnKindDefName ? Rand.Range(1f, 6.9f) : Rand.Range(0f, 20f);
            // 这里不能把非鼠族来访派系直接传给 PawnGenerator，否则会被派系的人种/Kind 规则污染，生成成普通智人。
            PawnGenerationRequest request = new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, allowDead: false, allowDowned: true, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, fixedBiologicalAge: randomAge, developmentalStages: DevelopmentalStage.Baby | DevelopmentalStage.Child, forceNoBackstory: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);
            if (LeadYourPetRules.ShouldAssignGeneratedTravelMouseEggFaction(
                hasGeneratedPawn: pawn != null,
                hasFaction: faction != null))
            {
                pawn.SetFaction(faction);
            }

            return pawn;
        }

        private static Pawn TryGenerateMouseDisasterTravelPawn(PawnKindDef kind, Faction faction)
        {
            if (!ShouldUseMouseDisasterTravelPawn() || MouseDisasterGenerateFactionRatkinPawnMethod == null)
            {
                return null;
            }

            try
            {
                Pawn pawn = OptionalModApi.Invoke(MouseDisasterGenerateFactionRatkinPawnMethod, kind, faction, DevelopmentalStage.Baby, 0.35f) as Pawn;
                if (pawn == null)
                {
                    return null;
                }

                if (kind.defName == MouseDisasterChildPawnKindDefName && MouseDisasterSetBiologicalAgeYearsMethod != null)
                {
                    MouseDisasterSetBiologicalAgeYearsMethod.Invoke(null, new object[] { pawn, Rand.Range(1f, 2.9f) });
                }

                PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);
                return pawn;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] MouseDisaster generation failed: " + exception, 19462001);
                return null;
            }
        }

        public static bool TryPrepareMouseDisasterTradablePrisoner(Pawn pawn, Faction ownerFaction)
        {
            if (MouseDisasterPrepareTradablePrisonerMethod == null || pawn == null || ownerFaction == null)
            {
                return false;
            }

            try
            {
                object result = MouseDisasterPrepareTradablePrisonerMethod.Invoke(null, new object[] { pawn, ownerFaction });
                return result is bool ok && ok;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Prisoner integration failed: " + exception, 19462002);
                return false;
            }
        }

        private static PawnKindDef ResolveTravelMouseEggPawnKind()
        {
            if (ShouldUseMouseDisasterTravelPawn())
            {
                PawnKindDef mouseDisasterKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(MouseDisasterChildPawnKindDefName);
                if (mouseDisasterKind != null)
                {
                    return mouseDisasterKind;
                }
            }

            return DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(x => x.race != null && string.Equals(x.race.defName, "Ratkin", StringComparison.OrdinalIgnoreCase) && !x.trader);
        }

        public static bool IsMouseDisasterTravelPawn(Pawn pawn)
        {
            return pawn?.kindDef != null && string.Equals(pawn.kindDef.defName, MouseDisasterChildPawnKindDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMouseDisasterTravelTrader(Pawn pawn)
        {
            return pawn?.kindDef != null && string.Equals(pawn.kindDef.defName, MouseDisasterTraderPawnKindDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMouseDisasterBeggarAdult(Pawn pawn)
        {
            return pawn?.kindDef != null && string.Equals(pawn.kindDef.defName, "MouseDisaster_BeggarRatkinAdult", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldUseMouseDisasterTravelPawn()
        {
            if (!ModsConfig.IsActive(MouseDisasterPackageId) && !ModsConfig.IsActive("nanaloveyuki.mouse.disaster.famine.continued"))
            {
                return false;
            }

            bool? beggarEnabled = TryGetMouseDisasterBoolProperty("AreBeggarIncidentsEnabled");
            bool? wildEnabled = TryGetMouseDisasterBoolProperty("AreWildIncidentsEnabled");
            bool? thiefEnabled = TryGetMouseDisasterBoolProperty("AreThiefIncidentsEnabled");

            if (beggarEnabled.HasValue || wildEnabled.HasValue || thiefEnabled.HasValue)
            {
                return beggarEnabled.GetValueOrDefault() || wildEnabled.GetValueOrDefault() || thiefEnabled.GetValueOrDefault();
            }

            return true;
        }

        private static bool? TryGetMouseDisasterBoolProperty(string propertyName)
        {
            Type utilityType = AccessTools.TypeByName("MouseDisaster.MouseDisasterUtility");
            PropertyInfo property = utilityType?.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool))
            {
                return null;
            }

            try
            {
                return (bool)property.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        public static bool ShouldDrag(LeashLink link)
        {
            if (link == null || link.Master == null || link.Pet == null || !link.Master.Spawned || !link.Pet.Spawned || link.Master.Map != link.Pet.Map)
            {
                return false;
            }

            Thing targetThing = GetCurrentFollowThing(link);
            IntVec3 targetCell = targetThing != null ? targetThing.Position : GetAnchorCell(link);
            if (!targetCell.IsValid)
            {
                targetCell = link.Master.Position;
            }

            float distance = targetCell.DistanceTo(link.Pet.Position);
            if ((link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !CanMouseEggMove(link.Pet))
            {
                return MasterControlsMovement(link) && distance >= MaxLeashLength - 0.5f;
            }

            if (distance < MaxLeashLength - 2f || !MasterControlsMovement(link))
            {
                return false;
            }

            float masterSpeed = Mathf.Max(1f, link.Master.TicksPerMoveCardinal);
            float petSpeed = Mathf.Max(1f, link.Pet.TicksPerMoveCardinal);
            return masterSpeed < petSpeed * 0.75f;
        }

        private static bool MasterControlsMovement(LeashLink link)
        {
            if (link.AnchorMode != LeashAnchorMode.None)
            {
                return true;
            }

            return link.Master != null && link.Master.pather != null && link.Master.pather.Moving;
        }

        public static bool CanMouseEggMove(Pawn pawn)
        {
            if (pawn == null || pawn.health?.capacities == null)
            {
                return false;
            }

            if (LifeStageUtility.AlwaysDowned(pawn))
            {
                return false;
            }

            return pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        public static bool MasterSleeping(Pawn master)
        {
            return master != null && (!master.Awake() || master.CurJobDef == JobDefOf.LayDown || master.CurJobDef == JobDefOf.LayDownResting || master.CurJobDef == JobDefOf.LayDownAwake);
        }

        public static bool MasterEating(Pawn master)
        {
            if (master == null)
            {
                return false;
            }

            if (master.CurJobDef == JobDefOf.Ingest)
            {
                return true;
            }

            return master.jobs?.curDriver is JobDriver_Ingest;
        }

        public static Thing GetCurrentFollowThing(LeashLink link)
        {
            if (link == null)
            {
                return null;
            }

            if (link.AnchorMode == LeashAnchorMode.Thing)
            {
                return link.AnchorThing;
            }

            return link.Master;
        }

        public static IntVec3 GetAnchorCell(LeashLink link)
        {
            if (link == null)
            {
                return IntVec3.Invalid;
            }

            if (link.AnchorMode == LeashAnchorMode.Thing && link.AnchorThing != null)
            {
                return link.AnchorThing.Position;
            }

            if (link.AnchorMode == LeashAnchorMode.Cell)
            {
                return link.AnchorCell;
            }

            return IntVec3.Invalid;
        }

        public static bool TryGetSpecialRenderData(Pawn pawn, out Vector3 drawPos, out float angle, out bool laying, out Rot4 facing)
        {
            drawPos = default(Vector3);
            angle = 0f;
            laying = false;
            facing = Rot4.South;

            if (pawn == null || Component == null)
            {
                return false;
            }

            if (Component.TryGetInteractionAnimationRenderData(pawn, out drawPos, out angle, out laying, out facing))
            {
                return true;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            if (link == null || !link.IsDragging || link.Master == null || !link.Master.Spawned)
            {
                return false;
            }

            Rot4 rot = link.Master.Rotation;
            drawPos = pawn.DrawPos;
            drawPos.y = AltitudeLayer.LayingPawn.AltitudeFor();
            angle = (rot == Rot4.West || rot == Rot4.North) ? 290f : 70f;
            laying = true;
            facing = Rot4.South;
            return true;
        }

        public static bool TryGetCustomRopeTarget(Pawn pawn, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (pawn == null || !pawn.Spawned || Component == null)
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            if (link == null)
            {
                return false;
            }

            Thing followThing = GetCurrentFollowThing(link);
            if (followThing != null && followThing != pawn && followThing.Spawned && followThing.Map == pawn.Map)
            {
                target = followThing;
                return true;
            }

            IntVec3 anchorCell = GetAnchorCell(link);
            if (anchorCell.IsValid && anchorCell.InBounds(pawn.Map))
            {
                target = anchorCell;
                return true;
            }

            return false;
        }

        public static Vector3 GetVisualDrawPos(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                if (TryGetSpecialRenderData(pawn, out Vector3 drawPos, out float _, out bool _, out Rot4 _))
                {
                    return drawPos;
                }

                return pawn.DrawPos;
            }

            return thing?.DrawPos ?? Vector3.zero;
        }

        public static void MaintainLeashMood(Pawn master, Pawn pet)
        {
            LeashLink link = Component?.GetLinkForPet(pet);
            LeashLinkKind kind = link?.Kind ?? LeashLinkKind.NormalPet;
            bool caretakerMood = kind == LeashLinkKind.MouseEggPet && ShouldUseCaretakerMood(pet);

            if (master?.needs?.mood?.thoughts?.memories != null)
            {
                if (!caretakerMood)
                {
                    master.needs.mood.thoughts.memories.TryGainMemoryFast(LeadYourPetDefOf.LeadYourPet_LeadingPetMood);
                }
                else
                {
                    master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_LeadingPetMood);
                }

                if (caretakerMood)
                {
                    master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_MouseEggOwnerMood);
                }
                else if (kind == LeashLinkKind.MouseEggPet)
                {
                    master.needs.mood.thoughts.memories.TryGainMemoryFast(LeadYourPetDefOf.LeadYourPet_MouseEggOwnerMood);
                }
                else
                {
                    master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_MouseEggOwnerMood);
                }
            }

            if (pet?.needs?.mood?.thoughts?.memories != null)
            {
                if (caretakerMood)
                {
                    pet.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_MouseEggFearMood);
                }
                else if (kind == LeashLinkKind.MouseEggPet)
                {
                    pet.needs.mood.thoughts.memories.TryGainMemoryFast(LeadYourPetDefOf.LeadYourPet_MouseEggFearMood);
                }
                else
                {
                    pet.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_MouseEggFearMood);
                }
            }
        }

        public static void ClearLeashMood(Pawn master, Pawn pet)
        {
            if (master?.needs?.mood?.thoughts?.memories != null)
            {
                master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_LeadingPetMood);
                master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_MouseEggOwnerMood);
            }

            if (pet?.needs?.mood?.thoughts?.memories != null)
            {
                pet.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_MouseEggFearMood);
            }
        }

        public static string InteractionLabel(LeadYourPetInteractionKind kind)
        {
            return InteractionLabel(null, kind);
        }

        public static string InteractionLabel(Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (IsColonistMouseEgg(pet))
            {
                switch (kind)
                {
                    case LeadYourPetInteractionKind.Kick: return "逗孩子跑两步";
                    case LeadYourPetInteractionKind.Observe: return "看看孩子";
                    case LeadYourPetInteractionKind.Drag: return "拉着孩子走";
                    case LeadYourPetInteractionKind.TapHead: return "拍拍孩子脑袋";
                    case LeadYourPetInteractionKind.PullTail: return "拽拽尾巴闹着玩";
                    case LeadYourPetInteractionKind.PullEar: return "捏捏耳朵";
                    case LeadYourPetInteractionKind.Whirl: return "抱起来转圈";
                    case LeadYourPetInteractionKind.Slap: return "推着转圈玩";
                    case LeadYourPetInteractionKind.Pat: return "轻轻拍一拍";
                    case LeadYourPetInteractionKind.KickButt: return "赶着孩子往前跑";
                    case LeadYourPetInteractionKind.WatchWork: return "让孩子看看大人在做事";
                    case LeadYourPetInteractionKind.Fed: return "喂孩子吃点东西";
                    case LeadYourPetInteractionKind.RestAtFeet: return "让孩子在脚边歇会";
                    case LeadYourPetInteractionKind.IdleNearOwner: return "让孩子在旁边待着";
                    case LeadYourPetInteractionKind.StudyFloor: return "陪孩子看地上的东西";
                    case LeadYourPetInteractionKind.SillySmile: return "逗孩子傻乐";
                    case LeadYourPetInteractionKind.TailPetting: return "顺顺尾巴";
                    case LeadYourPetInteractionKind.EarPetting: return "揉揉耳朵";
                    case LeadYourPetInteractionKind.OwnerWaited: return "等孩子慢慢跟上";
                    case LeadYourPetInteractionKind.Snack: return "给孩子零嘴";
                }
            }
            switch (kind)
            {
                case LeadYourPetInteractionKind.Kick: return "LeadYourPet_Interaction_Kick".Translate().Resolve();
                case LeadYourPetInteractionKind.Observe: return "LeadYourPet_Interaction_Observe".Translate().Resolve();
                case LeadYourPetInteractionKind.Drag: return "LeadYourPet_Interaction_Drag".Translate().Resolve();
                case LeadYourPetInteractionKind.TapHead: return "LeadYourPet_Interaction_TapHead".Translate().Resolve();
                case LeadYourPetInteractionKind.PullTail: return "LeadYourPet_Interaction_PullTail".Translate().Resolve();
                case LeadYourPetInteractionKind.PullEar: return "LeadYourPet_Interaction_PullEar".Translate().Resolve();
                case LeadYourPetInteractionKind.Whirl: return "LeadYourPet_Interaction_Whirl".Translate().Resolve();
                case LeadYourPetInteractionKind.Slap: return "LeadYourPet_Interaction_Slap".Translate().Resolve();
                case LeadYourPetInteractionKind.Pat: return "LeadYourPet_Interaction_Pat".Translate().Resolve();
                case LeadYourPetInteractionKind.KickButt: return "LeadYourPet_Interaction_KickButt".Translate().Resolve();
                case LeadYourPetInteractionKind.WatchWork: return "LeadYourPet_Interaction_WatchWork".Translate().Resolve();
                case LeadYourPetInteractionKind.Fed: return "LeadYourPet_Interaction_Fed".Translate().Resolve();
                case LeadYourPetInteractionKind.RestAtFeet: return "LeadYourPet_Interaction_RestAtFeet".Translate().Resolve();
                case LeadYourPetInteractionKind.IdleNearOwner: return "LeadYourPet_Interaction_IdleNearOwner".Translate().Resolve();
                case LeadYourPetInteractionKind.StudyFloor: return "LeadYourPet_Interaction_StudyFloor".Translate().Resolve();
                case LeadYourPetInteractionKind.SillySmile: return "LeadYourPet_Interaction_SillySmile".Translate().Resolve();
                case LeadYourPetInteractionKind.TailPetting: return "LeadYourPet_Interaction_TailPetting".Translate().Resolve();
                case LeadYourPetInteractionKind.EarPetting: return "LeadYourPet_Interaction_EarPetting".Translate().Resolve();
                case LeadYourPetInteractionKind.OwnerWaited: return "LeadYourPet_Interaction_OwnerWaited".Translate().Resolve();
                case LeadYourPetInteractionKind.Snack: return "LeadYourPet_Interaction_Snack".Translate().Resolve();
                default: return kind.ToString();
            }
        }

        public static string GetMouseEggDutyReport(LeashedMouseEggDutyState state)
        {
            switch (state)
            {
                case LeashedMouseEggDutyState.SleepAtLeash:
                    return "被牵着睡觉";
                case LeashedMouseEggDutyState.WaitForFeeding:
                    return "等待主人投喂";
                default:
                    return "跟随主人";
            }
        }

        public static bool TryGetMouseEggDutyReport(Pawn pawn, out string report)
        {
            report = null;
            if (pawn == null || Component == null)
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            if (link == null || link.Kind != LeashLinkKind.MouseEggPet)
            {
                return false;
            }

            report = GetMouseEggDutyReport(link.MouseEggDutyState);
            return !report.NullOrEmpty();
        }

        public static bool IsPositiveInteraction(LeadYourPetInteractionKind kind)
        {
            return (int)kind >= 10;
        }

        public static bool IsBidirectionalInteraction(LeadYourPetInteractionKind kind)
        {
            return BidirectionalInteractions.Contains(kind);
        }

        public static int OpinionOf(Pawn source, Pawn other)
        {
            return source?.relations?.OpinionOf(other) ?? 0;
        }

        public static bool CanUseSingleDirectionInteractions(Pawn master, Pawn pet)
        {
            return OpinionOf(master, pet) > 20 && OpinionOf(pet, master) > 20;
        }

        public static bool CanUseAsTraderChattel(Pawn pawn)
        {
            MouseEggState state = Component?.GetMouseEggState(pawn);
            return state != null && state.IsTravelStock && state.SellAsPrisoner;
        }

        public static bool ShouldBreakNonPlayerTravelLeashAfterAcquisition(LeashLink link)
        {
            if (link?.Pet == null || link.Master == null)
            {
                return false;
            }

            MouseEggState state = Component?.GetMouseEggState(link.Pet);
            return MouseEggOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: link.Master.Faction == Faction.OfPlayer,
                petIsPlayerOwned: link.Pet.Faction == Faction.OfPlayer,
                petIsColonyPrisoner: link.Pet.IsPrisonerOfColony,
                isTravelStock: state != null && state.IsTravelStock);
        }

        public static bool ShouldUseCaretakerMood(Pawn pet)
        {
            return IsColonistMouseEgg(pet);
        }

        public static bool ShouldBlockColonistBidirectionalInteraction(Pawn master, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (!IsBidirectionalInteraction(kind) || !IsColonistMouseEgg(pet))
            {
                return false;
            }

            return JoyIsFull(master) && RecreationNeedIsFull(pet);
        }

        public static bool JoyIsFull(Pawn pawn)
        {
            return pawn?.needs?.joy != null && pawn.needs.joy.CurLevelPercentage >= 0.999f;
        }

        public static bool RecreationNeedIsFull(Pawn pawn)
        {
            if (pawn?.needs == null)
            {
                return false;
            }

            if (pawn.needs.play != null)
            {
                return pawn.needs.play.CurLevelPercentage >= 0.999f;
            }

            return JoyIsFull(pawn);
        }

        public static IEnumerable<LeadYourPetInteractionKind> AvailableInteractionsFor(Pawn master, Pawn pet, IEnumerable<LeadYourPetInteractionKind> source)
        {
            foreach (LeadYourPetInteractionKind kind in source)
            {
                if (CanTriggerInteraction(master, pet, kind, out _))
                {
                    yield return kind;
                }
            }
        }

        public static bool CanTriggerInteraction(Pawn master, Pawn pet, LeadYourPetInteractionKind kind, out string reasonKey)
        {
            if (!CanPerformInteraction(pet, kind, out reasonKey))
            {
                return false;
            }

            if (ShouldBlockColonistBidirectionalInteraction(master, pet, kind))
            {
                reasonKey = null;
                return false;
            }

            return true;
        }

        public static bool CanPerformInteraction(Pawn pet, LeadYourPetInteractionKind kind, out string reasonKey)
        {
            reasonKey = null;
            switch (kind)
            {
                case LeadYourPetInteractionKind.PullTail:
                case LeadYourPetInteractionKind.TailPetting:
                    if (!HasRelevantBodyPart(pet, "tail"))
                    {
                        reasonKey = "LeadYourPet_Reason_MissingTail";
                        return false;
                    }

                    break;
                case LeadYourPetInteractionKind.PullEar:
                case LeadYourPetInteractionKind.EarPetting:
                    if (!HasRelevantBodyPart(pet, "ear"))
                    {
                        reasonKey = "LeadYourPet_Reason_MissingEar";
                        return false;
                    }

                    break;
                case LeadYourPetInteractionKind.TapHead:
                case LeadYourPetInteractionKind.Slap:
                    if (!HasRelevantBodyPart(pet, "head"))
                    {
                        reasonKey = "LeadYourPet_Reason_MissingHead";
                        return false;
                    }

                    break;
            }

            return true;
        }

        private static bool HasRelevantBodyPart(Pawn pawn, string token)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            return pawn.health.hediffSet.GetNotMissingParts().Any(x =>
                x.def.defName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Label.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool TryFindManualFeedFood(Pawn feeder, Pawn pet, out Thing foodSource, out string failReason)
        {
            foodSource = null;
            failReason = null;

            if (feeder == null || pet == null || !feeder.Spawned || !pet.Spawned || feeder.Map != pet.Map)
            {
                failReason = "LeadYourPet_Reason_NotOnCurrentMap";
                return false;
            }

            if (pet.needs?.food == null)
            {
                failReason = "LeadYourPet_Reason_NoFoodNeed";
                return false;
            }

            Thing inventoryFood = feeder.inventory?.innerContainer?.InnerListForReading?.FirstOrDefault(x => CanUseManualFeedFood(feeder, pet, x));
            if (inventoryFood != null)
            {
                foodSource = inventoryFood;
                return true;
            }

            ThingDef foodDef;
            Thing mapFood = FoodUtility.BestFoodSourceOnMap(feeder, pet, desperate: false, out foodDef, FoodPreferability.MealLavish, allowPlant: true, allowDrug: false, allowCorpse: false, allowDispenserFull: false, allowDispenserEmpty: false, allowForbidden: false, allowSociallyImproper: false, allowHarvest: false, forceScanWholeMap: true, ignoreReservations: true, calculateWantedStackCount: false, minPrefOverride: FoodPreferability.NeverForNutrition, minNutrition: null, allowVenerated: false);
            if (CanUseManualFeedFood(feeder, pet, mapFood))
            {
                foodSource = mapFood;
                return true;
            }

            failReason = "LeadYourPet_Reason_NoSuitableFood";
            return false;
        }

        public static bool TryFeedTarget(Pawn feeder, Pawn pet, out string failReason, out LeadYourPetInteractionKind interactionKind)
        {
            interactionKind = IsMouseEgg(pet) ? LeadYourPetInteractionKind.Fed : LeadYourPetInteractionKind.Snack;
            if (!TryFindManualFeedFood(feeder, pet, out Thing foodSource, out failReason))
            {
                return false;
            }

            float nutrition = FoodUtility.NutritionForEater(pet, foodSource);
            if (nutrition <= 0f)
            {
                failReason = "LeadYourPet_Reason_NoUsableNutrition";
                return false;
            }

            Thing consumed = foodSource.SplitOff(1);
            consumed?.Destroy();

            if (pet.needs?.food != null)
            {
                pet.needs.food.CurLevel = Mathf.Min(pet.needs.food.MaxLevel, pet.needs.food.CurLevel + nutrition);
            }

            Hediff malnutrition = pet.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition != null)
            {
                malnutrition.Severity = Mathf.Max(0f, malnutrition.Severity - nutrition * 0.15f);
            }

            failReason = null;
            return true;
        }

        public static bool TryShareMasterMealNutrition(Pawn master, Pawn pet, float nutritionIngested)
        {
            if (master == null || pet == null || nutritionIngested <= 0f || pet.needs?.food == null)
            {
                return false;
            }

            float sharedNutrition = LeadYourPetRules.ResolveSharedNutritionFromMasterMeal(nutritionIngested);
            if (sharedNutrition <= 0f)
            {
                return false;
            }

            pet.needs.food.CurLevel = LeadYourPetRules.ResolveFedNutritionLevel(
                pet.needs.food.CurLevel,
                pet.needs.food.MaxLevel,
                sharedNutrition);

            Hediff malnutrition = pet.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition != null)
            {
                malnutrition.Severity = Mathf.Max(0f, malnutrition.Severity - sharedNutrition * 0.15f);
            }

            return true;
        }

        public static bool ShouldTreatVisitorMouseEggPetLeashAsAggressive(Pawn master, Pawn pet)
        {
            return LeadYourPetRules.ShouldTreatVisitorMouseEggPetLeashAsAggressive(
                masterIsPlayer: master?.Faction == Faction.OfPlayer,
                petIsRatkinHumanlike: IsRatkinHumanlike(pet),
                petIsBabyOrChild: pet != null && (pet.DevelopmentalStage.Baby() || pet.DevelopmentalStage.Child()),
                petFactionExists: pet?.Faction != null,
                petFactionIsPlayer: pet?.Faction == Faction.OfPlayer,
                petFactionHostileToPlayer: pet?.Faction != null && Faction.OfPlayer != null && pet.Faction.HostileTo(Faction.OfPlayer),
                petInFriendlyVisitorLord: IsFriendlyVisitorMouseEgg(pet));
        }

        public static void ClearMouseDisasterVisitorCover(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            ClearMouseDisasterTradeAndVisitorFlags(pawn);
            TryRefreshMouseDisasterHiddenFactionRelations();
        }

        public static void ClearMouseDisasterTradeAndVisitorFlags(Pawn pawn)
        {
            TryInvokeVoid(MouseDisasterClearTradeLeaderStateMethod, pawn);
            TryInvokeVoid(MouseDisasterResetBeggarStateMethod, pawn);
        }

        public static bool TryMakeMouseDisasterVisitorsHostile(IEnumerable<Pawn> pawns, out int hostileCount)
        {
            hostileCount = 0;
            if (MouseDisasterTryMakeHostileVisitorsMethod == null)
            {
                return false;
            }

            try
            {
                object[] args = { pawns, 0 };
                bool handled = MouseDisasterTryMakeHostileVisitorsMethod.Invoke(null, args) is bool result && result;
                if (args.Length > 1 && args[1] is int count)
                {
                    hostileCount = count;
                }

                return handled;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Visitor integration failed: " + exception, 19462003);
                hostileCount = 0;
                return false;
            }
        }

        public static void MakeMouseDisasterFactionHostileToPlayer(Faction faction, bool explicitDriveAway)
        {
            if (MouseDisasterMakeFactionHostileToPlayerMethod == null)
            {
                return;
            }

            try
            {
                MouseDisasterMakeFactionHostileToPlayerMethod.Invoke(null, new object[] { faction, explicitDriveAway });
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Faction integration failed: " + exception, 19462004);
            }
        }

        public static void TryRefreshMouseDisasterHiddenFactionRelations()
        {
            TryInvokeVoid(MouseDisasterTryRefreshHiddenFactionRelationsMethod);
        }

        public static void NotifyMouseDisasterPawnIdentityChanged(Pawn pawn)
        {
            TryInvokeVoid(MouseDisasterNotifyIdentityChangedMethod, pawn);
        }

        public static void TryInvokeVoid(MethodInfo method, params object[] args)
        {
            if (method == null)
            {
                return;
            }

            try
            {
                OptionalModApi.Invoke(method, args);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Optional integration failed (" + method.Name + "): " + exception, 19462005);
            }
        }

        public static void ApplyInteractionMemories(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (pet?.needs?.mood?.thoughts?.memories == null)
            {
                return;
            }

            ThoughtDef ownerThought = OwnerThoughtFor(owner, pet, kind);
            if (ownerThought != null && owner?.needs?.mood?.thoughts?.memories != null)
            {
                owner.needs.mood.thoughts.memories.TryGainMemory(ownerThought, pet);
            }

            ThoughtDef petThought = PetThoughtFor(owner, pet, kind);
            if (petThought != null)
            {
                pet.needs.mood.thoughts.memories.TryGainMemory(petThought, owner);
            }
        }

        public static void ApplyInteractionJoy(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (!IsBidirectionalInteraction(kind))
            {
                return;
            }

            owner?.needs?.joy?.GainJoy(0.10f, JoyKindDefOf.Social);
            if (IsColonistMouseEgg(pet))
            {
                if (pet.needs?.play != null)
                {
                    pet.needs.play.Play(0.10f);
                }
                else
                {
                    pet?.needs?.joy?.GainJoy(0.10f, JoyKindDefOf.Social);
                }
            }
        }

        public static void ApplyInteractionPhysicalEffects(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if ((!IsBidirectionalInteraction(kind) && kind != LeadYourPetInteractionKind.Drag) || pet?.health == null)
            {
                return;
            }

            if (IsColonistMouseEgg(pet) && IsBidirectionalInteraction(kind))
            {
                return;
            }

            pet.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 0f, 0f, -1f, owner));
            if (Rand.Chance(0.35f))
            {
                BodyPartRecord part = FindBruiseBodyPart(pet, kind);
                Hediff hediff = HediffMaker.MakeHediff(LeadYourPetDefOf.LeadYourPet_DragBruise, pet, part);
                pet.health.AddHediff(hediff);
            }
        }

        private static BodyPartRecord FindBruiseBodyPart(Pawn pawn, LeadYourPetInteractionKind kind)
        {
            string token = null;
            switch (kind)
            {
                case LeadYourPetInteractionKind.TapHead:
                case LeadYourPetInteractionKind.Slap:
                    token = "head";
                    break;
                case LeadYourPetInteractionKind.PullEar:
                    token = "ear";
                    break;
                case LeadYourPetInteractionKind.PullTail:
                    token = "tail";
                    break;
                case LeadYourPetInteractionKind.KickButt:
                    token = "leg";
                    break;
            }

            if (!token.NullOrEmpty())
            {
                BodyPartRecord match = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def.defName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 || x.Label.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    return match;
                }
            }

            return pawn.health.hediffSet.GetNotMissingParts().RandomElementWithFallback();
        }

        private static bool CanUseManualFeedFood(Pawn feeder, Pawn pet, Thing food)
        {
            return food != null
                && food.def.IsNutritionGivingIngestible
                && food.IngestibleNow
                && !food.def.IsDrug
                && pet.WillEat(food, feeder)
                && FoodUtility.NutritionForEater(pet, food) > 0f;
        }

        public static ThoughtDef OwnerThoughtFor(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (IsColonistMouseEgg(pet) && IsBidirectionalInteraction(kind))
            {
                return LeadYourPetDefOf.LeadYourPet_OwnerPlayWithChild;
            }

            switch (kind)
            {
                case LeadYourPetInteractionKind.Kick: return LeadYourPetDefOf.LeadYourPet_OwnerKick;
                case LeadYourPetInteractionKind.Observe: return LeadYourPetDefOf.LeadYourPet_OwnerObserve;
                case LeadYourPetInteractionKind.Drag: return LeadYourPetDefOf.LeadYourPet_OwnerDrag;
                case LeadYourPetInteractionKind.TapHead: return LeadYourPetDefOf.LeadYourPet_OwnerTapHead;
                case LeadYourPetInteractionKind.PullTail: return LeadYourPetDefOf.LeadYourPet_OwnerPullTail;
                case LeadYourPetInteractionKind.PullEar: return LeadYourPetDefOf.LeadYourPet_OwnerPullEar;
                case LeadYourPetInteractionKind.Whirl: return LeadYourPetDefOf.LeadYourPet_OwnerWhirl;
                case LeadYourPetInteractionKind.Slap: return LeadYourPetDefOf.LeadYourPet_OwnerSlap;
                case LeadYourPetInteractionKind.Pat: return LeadYourPetDefOf.LeadYourPet_OwnerPat;
                case LeadYourPetInteractionKind.KickButt: return LeadYourPetDefOf.LeadYourPet_OwnerKickButt;
                default: return null;
            }
        }

        public static ThoughtDef PetThoughtFor(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (IsColonistMouseEgg(pet) && IsBidirectionalInteraction(kind))
            {
                return LeadYourPetDefOf.LeadYourPet_ChildPlayedWithCaretaker;
            }

            switch (kind)
            {
                case LeadYourPetInteractionKind.Kick: return LeadYourPetDefOf.LeadYourPet_PetKick;
                case LeadYourPetInteractionKind.Observe: return LeadYourPetDefOf.LeadYourPet_PetObserve;
                case LeadYourPetInteractionKind.Drag: return LeadYourPetDefOf.LeadYourPet_PetDrag;
                case LeadYourPetInteractionKind.TapHead: return LeadYourPetDefOf.LeadYourPet_PetTapHead;
                case LeadYourPetInteractionKind.PullTail: return LeadYourPetDefOf.LeadYourPet_PetPullTail;
                case LeadYourPetInteractionKind.PullEar: return LeadYourPetDefOf.LeadYourPet_PetPullEar;
                case LeadYourPetInteractionKind.Whirl: return LeadYourPetDefOf.LeadYourPet_PetWhirl;
                case LeadYourPetInteractionKind.Slap: return LeadYourPetDefOf.LeadYourPet_PetSlap;
                case LeadYourPetInteractionKind.Pat: return LeadYourPetDefOf.LeadYourPet_PetPat;
                case LeadYourPetInteractionKind.KickButt: return LeadYourPetDefOf.LeadYourPet_PetKickButt;
                case LeadYourPetInteractionKind.WatchWork: return LeadYourPetDefOf.LeadYourPet_PetWatchWork;
                case LeadYourPetInteractionKind.Fed: return LeadYourPetDefOf.LeadYourPet_PetFed;
                case LeadYourPetInteractionKind.RestAtFeet: return LeadYourPetDefOf.LeadYourPet_PetRestAtFeet;
                case LeadYourPetInteractionKind.IdleNearOwner: return LeadYourPetDefOf.LeadYourPet_PetIdleNearOwner;
                case LeadYourPetInteractionKind.StudyFloor: return LeadYourPetDefOf.LeadYourPet_PetStudyFloor;
                case LeadYourPetInteractionKind.SillySmile: return LeadYourPetDefOf.LeadYourPet_PetSillySmile;
                case LeadYourPetInteractionKind.TailPetting: return LeadYourPetDefOf.LeadYourPet_PetTailPetting;
                case LeadYourPetInteractionKind.EarPetting: return LeadYourPetDefOf.LeadYourPet_PetEarPetting;
                case LeadYourPetInteractionKind.OwnerWaited: return LeadYourPetDefOf.LeadYourPet_PetOwnerWaited;
                case LeadYourPetInteractionKind.Snack: return LeadYourPetDefOf.LeadYourPet_PetSnack;
                default: return null;
            }
        }
    }
}
