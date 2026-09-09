using Xunit;

namespace LeadYourPet.Tests
{
    public class MouseEggOwnershipStateMachineTests
    {
        [Fact]
        public void ClearsTravelStateAfterPurchase()
        {
            MouseEggOwnershipSnapshot current = new MouseEggOwnershipSnapshot(
                isPet: true,
                isTravelStock: true,
                sellAsPrisoner: true,
                hasCurrentMaster: true);

            MouseEggOwnershipSnapshot next = MouseEggOwnershipStateMachine.ResolveBoughtTravelMouseEggState(current);

            Assert.False(next.IsPet);
            Assert.False(next.IsTravelStock);
            Assert.False(next.SellAsPrisoner);
            Assert.False(next.HasCurrentMaster);
        }

        [Fact]
        public void DetachesNonPlayerLeashAfterPlayerBuysTravelMouseEgg()
        {
            Assert.True(MouseEggOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: false,
                petIsPlayerOwned: true,
                petIsColonyPrisoner: false,
                isTravelStock: true));
        }

        [Fact]
        public void KeepsLeashWhenPawnWasNotTravelStock()
        {
            Assert.False(MouseEggOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: false,
                petIsPlayerOwned: true,
                petIsColonyPrisoner: false,
                isTravelStock: false));
        }

        [Fact]
        public void PreservesPawnIdentityWhenRegisteringTravelMouseEggBeforePurchase()
        {
            Assert.False(MouseEggOwnershipStateMachine.ShouldClearPawnFactionForTravelRegistration(
                preserveOwnership: false,
                pawnSameFactionAsCarrier: true));

            Assert.False(MouseEggOwnershipStateMachine.ShouldForceGuestSlaveForTravelRegistration(
                preserveOwnership: false,
                hasGuestTracker: true));
        }

        [Fact]
        public void HandlesBoughtTravelMouseEggOnlyForPlayerBuysOfTravelStock()
        {
            Assert.True(MouseEggOwnershipStateMachine.ShouldHandleBoughtTravelMouseEgg(
                actionIsPlayerBuys: true,
                wasTravelStock: true,
                wasSellable: true));

            Assert.False(MouseEggOwnershipStateMachine.ShouldHandleBoughtTravelMouseEgg(
                actionIsPlayerBuys: true,
                wasTravelStock: false,
                wasSellable: true));

            Assert.False(MouseEggOwnershipStateMachine.ShouldHandleBoughtTravelMouseEgg(
                actionIsPlayerBuys: false,
                wasTravelStock: true,
                wasSellable: true));
        }
    }
}
