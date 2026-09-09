using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        private void ProcessLink(LeashLink link, int ticksGame)
        {
            if (link == null || link.Master == null || link.Pet == null)
            {
                return;
            }

            bool forceImmediateUpdate = link.ForceImmediateUpdate;
            link.ForceImmediateUpdate = false;

            if (link.Pet.CarriedBy != null
                && link.Pet.CarriedBy.carryTracker != null
                && link.Pet.CarriedBy.carryTracker.CarriedThing == link.Pet)
            {
                bool dropSucceeded = false;
                if (LeadYourPetUtility.ShouldBlockCarryOfProtectedMouseEgg(link.Pet.CarriedBy, link.Pet))
                {
                    dropSucceeded = link.Pet.CarriedBy.carryTracker.TryDropCarriedThing(link.Pet.CarriedBy.Position, ThingPlaceMode.Near, out Thing _);
                }
                else
                {
                    return;
                }

                if (LeadYourPetRules.ShouldPauseLeashForCarriedPet(
                    petWasCarried: true,
                    dropSucceeded: dropSucceeded,
                    petStillUnavailable: link.Pet.CarriedBy != null || !link.Pet.Spawned))
                {
                    link.ForceImmediateUpdate = true;
                    return;
                }
            }

            if (LeadYourPetUtility.ShouldBreakNonPlayerTravelLeashAfterAcquisition(link))
            {
                EndLink(link, false);
                return;
            }

            if (ShouldBreak(link))
            {
                EndLink(link, false);
                return;
            }

            if (link.Kind == LeashLinkKind.MouseEggPet && !LeadYourPetUtility.CanBePetMouseEgg(link.Pet, out _))
            {
                ClearMouseEggPetState(link.Pet);
                return;
            }

            if (link.Kind == LeashLinkKind.RatkinMotherBaby && !LeadYourPetUtility.CanStartRatkinMotherLeash(link.Master, link.Pet, out _))
            {
                EndLink(link, false);
                return;
            }

            ValidateAnchor(link);
            if (ticksGame - link.LastMoodRefreshTick >= 180)
            {
                link.LastMoodRefreshTick = ticksGame;
                LeadYourPetUtility.MaintainLeashMood(link.Master, link.Pet);
            }

            if (HasBlockingInteractionAnimation(link.Pet))
            {
                link.Pet.pather?.StopDead();
                return;
            }

            bool sleepingMaster = HandleSleepingMaster(link);
            bool runExpensiveUpdate = ShouldRunExpensiveLinkUpdate(link, ticksGame, forceImmediateUpdate);
            if (link.Kind == LeashLinkKind.MouseEggPet)
            {
                link.MouseEggDutyState = LeadYourPetRules.ResolveLeashedMouseEggDutyState(
                    masterSleeping: sleepingMaster,
                    masterEating: LeadYourPetUtility.MasterEating(link.Master));
                UpdateMouseEggDutyReport(link);
            }

            bool draggingNow = LeadYourPetUtility.ShouldDrag(link);
            if (draggingNow && !link.IsDragging)
            {
                link.CachedHasValidAutoInteractionPool = false;
                link.Pet.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 0f, 0f, -1f, link.Master));
                if (link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby)
                {
                    ApplyInteraction(link.Master, link.Pet, LeadYourPetInteractionKind.Drag, true);
                }

                if (Rand.Chance(0.08f))
                {
                    AddDragBruise(link.Pet);
                }
            }

            link.IsDragging = draggingNow;
            MaintainDraggedSlow(link.Pet, draggingNow);

            IntVec3 targetCell = ResolveTargetCell(link);
            if (!targetCell.IsValid)
            {
                EndLink(link, false);
                return;
            }

            float distance = targetCell.DistanceTo(link.Pet.Position);
            if (distance > LeadYourPetUtility.HardLeashLength)
            {
                TightPullBack(link, targetCell);
                distance = targetCell.DistanceTo(link.Pet.Position);
            }

            if ((link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !LeadYourPetUtility.CanMouseEggMove(link.Pet) && draggingNow)
            {
                PullImmobileMouseEgg(link, targetCell);
            }

            if (distance > LeadYourPetUtility.MaxLeashLength || draggingNow)
            {
                if (runExpensiveUpdate)
                {
                    GentlePullBack(link, targetCell, ticksGame, forceImmediateUpdate);
                }
            }

            if (!sleepingMaster && (distance > LeadYourPetUtility.FollowThreshold || draggingNow || ShouldTargetMovementRefresh(link)))
            {
                if (runExpensiveUpdate)
                {
                    UpdateMouseEggDutyReport(link);
                    ForceFollow(link, ticksGame, forceImmediateUpdate);
                }
            }

            if (link.Kind == LeashLinkKind.MouseEggPet
                && link.MouseEggDutyState != LeashedMouseEggDutyState.FollowMaster
                && !draggingNow
                && distance <= LeadYourPetUtility.MaxLeashLength)
            {
                link.Pet.pather?.StopDead();
                return;
            }

            if (runExpensiveUpdate)
            {
                link.LastExpensiveUpdateTick = ticksGame;

                if (link.Kind == LeashLinkKind.MouseEggPet)
                {
                    ProcessMouseEggPet(link, draggingNow, distance, ticksGame, sleepingMaster);
                }
                else if (link.Kind == LeashLinkKind.NormalPet)
                {
                    TryFeedPet(link, ticksGame);
                }
            }
        }

        private bool ShouldRunExpensiveLinkUpdate(LeashLink link, int ticksGame, bool forceImmediateUpdate)
        {
            return LeadYourPetRules.ShouldRunExpensiveLinkUpdate(
                currentTick: ticksGame,
                lastExpensiveUpdateTick: link.LastExpensiveUpdateTick,
                minIntervalTicks: 60,
                forceImmediate: forceImmediateUpdate);
        }

        private void ProcessMouseEggPet(LeashLink link, bool draggingNow, float distance, int ticksGame, bool sleepingMaster)
        {
            bool playerControlled = link.Master.Faction == Faction.OfPlayer;
            bool canUseSingleDirection = LeadYourPetUtility.CanUseSingleDirectionInteractions(link.Master, link.Pet);
            if (ticksGame >= link.NextAutoInteractionTick && distance <= 5.9f && !draggingNow)
            {
                link.NextAutoInteractionTick = ticksGame + NextAutoInteractionDelay(playerControlled);
                if (playerControlled || Rand.Chance(0.03f))
                {
                    List<LeadYourPetInteractionKind> pool = GetCachedAutoInteractionPool(link, ticksGame, playerControlled, canUseSingleDirection, sleepingMaster);
                    if (pool.Count > 0)
                    {
                        LeadYourPetInteractionKind kind = pool.RandomElement();
                        ApplyInteraction(link.Master, link.Pet, kind, LeadYourPetUtility.IsBidirectionalInteraction(kind));
                    }
                }
            }

            TryFeedPet(link, ticksGame);

            if (!playerControlled)
            {
                return;
            }

            if (canUseSingleDirection && ticksGame - link.LastObserveMemoryTick > 1800 && distance <= 4.9f)
            {
                int observers = CountNearbyHumanlikeObservers(link.Master, link.Pet, 4f, 2);
                if (observers >= 2 && LeadYourPetUtility.CanPerformInteraction(link.Pet, LeadYourPetInteractionKind.Observe, out _))
                {
                    link.LastObserveMemoryTick = ticksGame;
                    ApplyInteraction(link.Master, link.Pet, LeadYourPetInteractionKind.Observe, false);
                }
            }
        }

        private List<LeadYourPetInteractionKind> GetCachedAutoInteractionPool(LeashLink link, int ticksGame, bool playerControlled, bool canUseSingleDirection, bool masterSleeping)
        {
            if (link.CachedAutoInteractionPool == null)
            {
                link.CachedAutoInteractionPool = new List<LeadYourPetInteractionKind>();
            }

            if (!LeadYourPetRules.ShouldRefreshAutoInteractionPool(
                currentTick: ticksGame,
                lastPoolTick: link.LastAutoInteractionPoolTick,
                cacheIntervalTicks: 300,
                cacheValid: link.CachedHasValidAutoInteractionPool,
                cachedPlayerControlled: link.CachedPlayerControlled,
                playerControlled: playerControlled,
                cachedCanUseSingleDirection: link.CachedCanUseSingleDirectionInteractions,
                canUseSingleDirection: canUseSingleDirection,
                cachedMasterSleeping: link.CachedMasterSleeping,
                masterSleeping: masterSleeping))
            {
                return link.CachedAutoInteractionPool;
            }

            IEnumerable<LeadYourPetInteractionKind> interactionSource;
            if (!playerControlled || canUseSingleDirection)
            {
                interactionSource = LeadYourPetUtility.AutomaticPositiveInteractions;
            }
            else if (LeadYourPetRules.ShouldUseBidirectionalMouseEggAutoInteractions(playerControlled, canUseSingleDirection, masterSleeping))
            {
                interactionSource = LeadYourPetUtility.BidirectionalInteractions;
            }
            else
            {
                interactionSource = Enumerable.Empty<LeadYourPetInteractionKind>();
            }

            link.CachedAutoInteractionPool.Clear();
            link.CachedAutoInteractionPool.AddRange(
                LeadYourPetUtility.AvailableInteractionsFor(link.Master, link.Pet, interactionSource));
            link.CachedPlayerControlled = playerControlled;
            link.CachedCanUseSingleDirectionInteractions = canUseSingleDirection;
            link.CachedMasterSleeping = masterSleeping;
            link.CachedHasValidAutoInteractionPool = true;
            link.LastAutoInteractionPoolTick = ticksGame;
            return link.CachedAutoInteractionPool;
        }

        private int NextAutoInteractionDelay(bool playerControlled)
        {
            return playerControlled ? Rand.RangeInclusive(1800, 3600) : Rand.RangeInclusive(4200, 7200);
        }

        private int CountNearbyHumanlikeObservers(Pawn master, Pawn pet, float radius, int earlyExitCount)
        {
            if (master?.Map?.mapPawns?.AllPawnsSpawned == null || pet == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<Pawn> spawnedPawns = master.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                Pawn pawn = spawnedPawns[i];
                if (pawn == null || pawn == master || !pawn.RaceProps.Humanlike)
                {
                    continue;
                }

                if (!pawn.Position.InHorDistOf(pet.Position, radius))
                {
                    continue;
                }

                count++;
                if (count >= earlyExitCount)
                {
                    return count;
                }
            }

            return count;
        }

        private bool HandleSleepingMaster(LeashLink link)
        {
            if (link.AnchorMode != LeashAnchorMode.None)
            {
                link.MasterSleepSuspended = false;
                return false;
            }

            if (LeadYourPetUtility.MasterSleeping(link.Master))
            {
                link.MasterSleepSuspended = true;
                return true;
            }

            if (link.MasterSleepSuspended)
            {
                link.MasterSleepSuspended = false;
                TightPullBack(link, link.Master.Position);
            }

            return false;
        }

        private void ValidateAnchor(LeashLink link)
        {
            if (link.AnchorMode == LeashAnchorMode.Thing)
            {
                if (link.AnchorThing == null || !link.AnchorThing.Spawned || link.AnchorThing.Map != link.Pet.Map)
                {
                    link.AnchorMode = LeashAnchorMode.None;
                    link.AnchorThing = null;
                }
            }
            else if (link.AnchorMode == LeashAnchorMode.Cell)
            {
                if (!link.AnchorCell.IsValid || !link.AnchorCell.InBounds(link.Pet.Map))
                {
                    link.AnchorMode = LeashAnchorMode.None;
                    link.AnchorCell = IntVec3.Invalid;
                }
            }
        }

        private IntVec3 ResolveTargetCell(LeashLink link)
        {
            Thing followThing = LeadYourPetUtility.GetCurrentFollowThing(link);
            if (followThing != null)
            {
                return followThing.Position;
            }

            if (link.AnchorMode == LeashAnchorMode.Cell)
            {
                return link.AnchorCell;
            }

            return link.Master?.Position ?? IntVec3.Invalid;
        }

        private bool ShouldTargetMovementRefresh(LeashLink link)
        {
            if (link.AnchorMode == LeashAnchorMode.Thing)
            {
                return link.AnchorThing is Pawn anchorPawn && anchorPawn.pather != null && anchorPawn.pather.Moving;
            }

            if (link.AnchorMode == LeashAnchorMode.None)
            {
                return link.Master.pather != null && link.Master.pather.Moving;
            }

            return false;
        }

        private void AddDragBruise(Pawn pet)
        {
            if (pet.health == null)
            {
                return;
            }

            BodyPartRecord part = pet.health.hediffSet.GetNotMissingParts().RandomElementWithFallback();
            Hediff hediff = HediffMaker.MakeHediff(LeadYourPetDefOf.LeadYourPet_DragBruise, pet, part);
            pet.health.AddHediff(hediff);
        }

        private void MaintainDraggedSlow(Pawn pet, bool dragging)
        {
            if (pet?.health == null)
            {
                return;
            }

            Hediff existing = pet.health.hediffSet.GetFirstHediffOfDef(LeadYourPetDefOf.LeadYourPet_DragSlow);
            if (dragging)
            {
                if (existing == null)
                {
                    pet.health.AddHediff(HediffMaker.MakeHediff(LeadYourPetDefOf.LeadYourPet_DragSlow, pet));
                }
            }
            else if (existing != null)
            {
                pet.health.RemoveHediff(existing);
            }
        }

        private void TryFeedPet(LeashLink link, int ticksGame)
        {
            if (link.Master == null || link.Pet == null || !link.Master.Spawned || !link.Pet.Spawned)
            {
                return;
            }

            if (ticksGame % 120 != 0)
            {
                return;
            }

            if (link.Kind == LeashLinkKind.MouseEggPet && !LeadYourPetUtility.CanUseSingleDirectionInteractions(link.Master, link.Pet))
            {
                return;
            }

            if (link.MouseEggDutyState == LeashedMouseEggDutyState.WaitForFeeding)
            {
                return;
            }

            Hediff malnutrition = link.Pet.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            bool hungryEnough = link.Pet.needs?.food != null && link.Pet.needs.food.CurLevelPercentage <= 0.35f;
            if (!hungryEnough && (malnutrition == null || malnutrition.Severity < 0.05f))
            {
                return;
            }

            if (!LeadYourPetUtility.TryFeedTarget(link.Master, link.Pet, out string _, out LeadYourPetInteractionKind interactionKind))
            {
                return;
            }

            ApplyInteraction(link.Master, link.Pet, interactionKind, false);
        }

        private void GentlePullBack(LeashLink link, IntVec3 targetCell, int ticksGame, bool forceImmediateUpdate)
        {
            if ((link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !LeadYourPetUtility.CanMouseEggMove(link.Pet))
            {
                return;
            }

            if (!targetCell.IsValid)
            {
                return;
            }

            IntVec3 moveCell = targetCell;
            if (link.AnchorMode == LeashAnchorMode.None && link.Master != null)
            {
                moveCell = link.Master.Position - link.Master.Rotation.FacingCell;
            }

            if (!LeadYourPetRules.ShouldIssueFollowPath(
                currentTick: ticksGame,
                lastFollowIssueTick: link.LastFollowIssueTick,
                minIntervalTicks: 30,
                forceImmediate: forceImmediateUpdate))
            {
                return;
            }

            if (moveCell.IsValid && moveCell.InBounds(link.Pet.Map) && moveCell.Standable(link.Pet.Map) && link.Pet.CanReach(moveCell, PathEndMode.OnCell, Danger.Deadly))
            {
                link.Pet.pather.StartPath(moveCell, PathEndMode.OnCell);
                link.LastFollowIssueTick = ticksGame;
            }
        }

        private void TightPullBack(LeashLink link, IntVec3 targetCell)
        {
            if (link.Pet == null || !link.Pet.Spawned || !targetCell.IsValid || !targetCell.InBounds(link.Pet.Map))
            {
                return;
            }

            IntVec3 nearCell = CellFinder.RandomClosewalkCellNear(targetCell, link.Pet.Map, 1);
            if (!nearCell.IsValid)
            {
                nearCell = targetCell;
            }

            link.Pet.pather?.StopDead();
            link.Pet.Position = nearCell;
            link.Pet.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
        }

        private void ForceFollow(LeashLink link, int ticksGame, bool forceImmediateUpdate)
        {
            if (link == null || link.Pet == null)
            {
                return;
            }

            bool masterIsPlayerControlled = link.Master != null && link.Master.Faction == Faction.OfPlayer;
            if (!masterIsPlayerControlled && IsRunningFollowMasterJob(link))
            {
                link.Pet.jobs?.EndCurrentJob(JobCondition.Succeeded, false, true);
            }

            if ((link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !LeadYourPetUtility.CanMouseEggMove(link.Pet))
            {
                return;
            }

            bool shouldIssueFollowPath = LeadYourPetRules.ShouldIssueFollowPath(
                currentTick: ticksGame,
                lastFollowIssueTick: link.LastFollowIssueTick,
                minIntervalTicks: 30,
                forceImmediate: forceImmediateUpdate);
            if (!shouldIssueFollowPath)
            {
                return;
            }

            Thing followThing = LeadYourPetUtility.GetCurrentFollowThing(link);
            if (followThing != null && link.Pet.CanReach(followThing, PathEndMode.Touch, Danger.Deadly))
            {
                if (LeadYourPetRules.ShouldIssueLeashFollowJob(
                    masterIsPlayerControlled: masterIsPlayerControlled,
                    followTargetIsMaster: followThing == link.Master,
                    alreadyFollowingSameMaster: IsRunningFollowMasterJob(link),
                    canReachTarget: true,
                    shouldIssueFollowPath: shouldIssueFollowPath))
                {
                    Job followJob = JobMaker.MakeJob(LeadYourPetDefOf.LeadYourPet_FollowMaster, link.Master);
                    followJob.playerForced = false;
                    if (link.Pet.jobs != null && link.Pet.jobs.TryTakeOrderedJob(followJob, JobTag.Misc, false))
                    {
                        link.LastFollowIssueTick = ticksGame;
                        return;
                    }
                }

                link.Pet.pather.StartPath(followThing, PathEndMode.Touch);
                link.LastFollowIssueTick = ticksGame;
                return;
            }

            if (link.AnchorMode == LeashAnchorMode.Cell && link.AnchorCell.IsValid && link.Pet.CanReach(link.AnchorCell, PathEndMode.OnCell, Danger.Deadly))
            {
                link.Pet.pather.StartPath(link.AnchorCell, PathEndMode.OnCell);
                link.LastFollowIssueTick = ticksGame;
            }
        }

        private bool IsRunningFollowMasterJob(LeashLink link)
        {
            return link?.Pet?.CurJobDef == LeadYourPetDefOf.LeadYourPet_FollowMaster
                && link.Pet.CurJob != null
                && link.Pet.CurJob.targetA.Thing == link.Master;
        }

        private void UpdateMouseEggDutyReport(LeashLink link)
        {
            if (link?.Pet?.CurJob == null || link.Kind != LeashLinkKind.MouseEggPet)
            {
                return;
            }

            if (link.MouseEggDutyState == LeashedMouseEggDutyState.FollowMaster)
            {
                if (link.Pet.CurJob.reportStringOverride == LeadYourPetUtility.GetMouseEggDutyReport(LeashedMouseEggDutyState.SleepAtLeash)
                    || link.Pet.CurJob.reportStringOverride == LeadYourPetUtility.GetMouseEggDutyReport(LeashedMouseEggDutyState.WaitForFeeding))
                {
                    link.Pet.CurJob.reportStringOverride = null;
                }

                return;
            }

            link.Pet.CurJob.reportStringOverride = LeadYourPetUtility.GetMouseEggDutyReport(link.MouseEggDutyState);
        }

        public void TightenMouseEggLeashes(Pawn master)
        {
            foreach (LeashLink link in GetLinksForMaster(master))
            {
                if (link.Kind != LeashLinkKind.MouseEggPet && link.Kind != LeashLinkKind.RatkinMotherBaby)
                {
                    continue;
                }

                if (!link.Pet.Spawned || link.Pet.Map != master.Map)
                {
                    continue;
                }

                TightPullBack(link, master.Position);
            }
        }

        private void PullImmobileMouseEgg(LeashLink link, IntVec3 targetCell)
        {
            if (link.Pet == null || !link.Pet.Spawned || !targetCell.IsValid)
            {
                return;
            }

            IntVec3 pullCell = targetCell;
            if (link.AnchorMode == LeashAnchorMode.None && link.Master != null)
            {
                pullCell = link.Master.Position - (link.Master.Rotation.FacingCell * 2);
                if (!pullCell.InBounds(link.Master.Map) || !pullCell.Standable(link.Master.Map))
                {
                    pullCell = link.Master.Position - link.Master.Rotation.FacingCell;
                }

                if (!pullCell.InBounds(link.Master.Map) || !pullCell.Standable(link.Master.Map))
                {
                    pullCell = CellFinder.RandomClosewalkCellNear(link.Master.Position, link.Master.Map, 2);
                }
            }
            else if (!pullCell.InBounds(link.Pet.Map) || !pullCell.Standable(link.Pet.Map))
            {
                pullCell = CellFinder.RandomClosewalkCellNear(targetCell, link.Pet.Map, 1);
            }

            if (!pullCell.IsValid || pullCell == link.Pet.Position)
            {
                return;
            }

            link.Pet.pather?.StopDead();
            link.Pet.Position = pullCell;
            link.Pet.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
        }
    }
}
