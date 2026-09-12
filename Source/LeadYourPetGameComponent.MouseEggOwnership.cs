using RimWorld;
using Verse;

using Verse.AI;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public void MarkAsTravelMouseEgg(Pawn pawn, Pawn carrier, Pawn trader, bool sellAsPrisoner, bool preserveOwnership = false)
        {
            if (!LeadYourPetUtility.CanBePetMouseEgg(pawn, out _))
            {
                return;
            }

            MouseEggState state = EnsureMouseEggState(pawn);
            ApplyOwnershipSnapshot(state, new MouseEggOwnershipSnapshot(
                isPet: true,
                isTravelStock: true,
                sellAsPrisoner: sellAsPrisoner,
                hasCurrentMaster: carrier != null));
            state.CurrentMaster = carrier;
            if (MouseEggOwnershipStateMachine.ShouldClearPawnFactionForTravelRegistration(
                preserveOwnership,
                pawn.Faction == carrier?.Faction))
            {
                pawn.SetFaction(null);
            }

            if (MouseEggOwnershipStateMachine.ShouldForceGuestSlaveForTravelRegistration(
                preserveOwnership,
                pawn.guest != null))
            {
                pawn.guest.joinStatus = JoinStatus.JoinAsSlave;
            }

            TryDropCarriedPawnAfterBecomingMouseEggPet(pawn);
        }

        public void ClearTravelStock(Pawn pawn)
        {
            MouseEggState state = GetMouseEggState(pawn);
            if (state == null)
            {
                return;
            }

            ApplyOwnershipSnapshot(state, MouseEggOwnershipStateMachine.ResolveBoughtTravelMouseEggState(ToOwnershipSnapshot(state)));
            PruneMouseEggStateIfEmpty(pawn);
        }

        public void HandleBoughtTravelMouseEgg(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            EndLeashForPet(pawn, false);

            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);
            if (pawn.guest != null)
            {
                pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                pawn.guest.resistance = LeadYourPetRules.ResolveTravelMouseEggPrisonerResistance();
                pawn.guest.will = LeadYourPetRules.ResolveTravelMouseEggPrisonerWill();
            }

            EndLeashForPet(pawn, false);
            TryMoveBoughtPrisonerNearPrisonBed(pawn);
            ClearTravelStock(pawn);
            LeadYourPetUtility.ClearMouseDisasterVisitorCover(pawn);
            LeadYourPetUtility.NotifyMouseDisasterPawnIdentityChanged(pawn);
        }

        private static MouseEggOwnershipSnapshot ToOwnershipSnapshot(MouseEggState state)
        {
            return new MouseEggOwnershipSnapshot(
                isPet: state != null && state.IsPet,
                isTravelStock: state != null && state.IsTravelStock,
                sellAsPrisoner: state != null && state.SellAsPrisoner,
                hasCurrentMaster: state != null && state.CurrentMaster != null);
        }

        private static void ApplyOwnershipSnapshot(MouseEggState state, MouseEggOwnershipSnapshot snapshot)
        {
            if (state == null)
            {
                return;
            }

            state.IsPet = snapshot.IsPet;
            state.IsTravelStock = snapshot.IsTravelStock;
            state.SellAsPrisoner = snapshot.SellAsPrisoner;
            state.CurrentMaster = snapshot.HasCurrentMaster ? state.CurrentMaster : null;
        }

        private void PruneMouseEggStateIfEmpty(Pawn pawn)
        {
            MouseEggState state = GetMouseEggState(pawn);
            if (state == null)
            {
                return;
            }

            if (!state.IsPet && state.CurrentMaster == null && !state.IsTravelStock)
            {
                mouseEggStates.Remove(state);
                UnregisterMouseEggState(state);
            }
        }

        private void RegisterMouseEggState(MouseEggState state)
        {
            if (state?.Pawn == null)
            {
                return;
            }

            mouseEggStateByPawn[state.Pawn] = state;
        }

        private void UnregisterMouseEggState(MouseEggState state)
        {
            if (state?.Pawn == null)
            {
                return;
            }

            mouseEggStateByPawn.Remove(state.Pawn);
        }

        private void TryDropCarriedPawnAfterBecomingMouseEggPet(Pawn pawn)
        {
            Pawn carrier = pawn?.CarriedBy;
            Pawn_CarryTracker carryTracker = carrier?.carryTracker;
            EndExternalToddlerHoldJobs(pawn, carrier);
            if (!LeadYourPetRules.ShouldDropCarriedPawnAfterBecomingMouseEggPet(
                isProtectedMouseEggPet: LeadYourPetUtility.IsProtectedLeashedMouseEgg(pawn),
                pawnHasCarrier: carrier != null,
                carrierHasCarryTracker: carryTracker != null,
                carrierIsCarryingPawn: carryTracker?.CarriedThing == pawn))
            {
                return;
            }

            carryTracker.TryDropCarriedThing(carrier.Position, ThingPlaceMode.Near, out Thing _);
        }

        private void EndExternalToddlerHoldJobs(Pawn pawn, Pawn carrier)
        {
            if (LeadYourPetUtility.ShouldEndExternalToddlerHoldJob(pawn, pawn))
            {
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }

            if (LeadYourPetUtility.ShouldEndExternalToddlerHoldJob(carrier, pawn))
            {
                carrier.jobs?.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }
        }
    }
}
