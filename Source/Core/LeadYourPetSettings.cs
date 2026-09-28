using Verse;

namespace LeadYourPet
{
    public class LeadYourPetSettings : ModSettings
    {
        public const int DefaultMinPetAgeYears = 0;
        public const int DefaultMaxPetAgeYears = 14;
        public const int AbsoluteMaxPetAgeYears = 100;
        public const int DefaultOrdinaryTravelRatkinYoungCount = 2;
        public const int MaxOrdinaryTravelRatkinYoungCount = 8;

        public int maxLeashLength = 10;
        public int maxMouseEggPetLeashStartDistance = 10;
        public int minPetAgeYears = DefaultMinPetAgeYears;
        public int maxPetAgeYears = DefaultMaxPetAgeYears;
        public int ordinaryTravelRatkinYoungCount = DefaultOrdinaryTravelRatkinYoungCount;
        public bool infiniteLeash;
        public bool showInteractionText = true;
        public bool allowAnimalPets = true;
        public bool allowRatkinYoungPets = true;
        public bool visitorsLeadRatkinYoung = true;
        public LeashedPawnMapExitBehavior leashedPawnMapExitBehavior = LeashedPawnMapExitBehavior.StayInPlace;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxLeashLength, "maxLeashLength", 10);
            Scribe_Values.Look(ref maxMouseEggPetLeashStartDistance, "maxMouseEggPetLeashStartDistance", 10);
            Scribe_Values.Look(ref minPetAgeYears, "minPetAgeYears", DefaultMinPetAgeYears);
            Scribe_Values.Look(ref maxPetAgeYears, "maxPetAgeYears", DefaultMaxPetAgeYears);
            Scribe_Values.Look(ref ordinaryTravelRatkinYoungCount, "ordinaryTravelRatkinYoungCount", DefaultOrdinaryTravelRatkinYoungCount);
            Scribe_Values.Look(ref infiniteLeash, "infiniteLeash", false);
            Scribe_Values.Look(ref showInteractionText, "showInteractionText", true);
            Scribe_Values.Look(ref allowAnimalPets, "allowAnimalPets", true);
            Scribe_Values.Look(ref allowRatkinYoungPets, "allowRatkinYoungPets", true);
            Scribe_Values.Look(ref visitorsLeadRatkinYoung, "visitorsLeadRatkinYoung", true);
            Scribe_Values.Look(ref leashedPawnMapExitBehavior, "leashedPawnMapExitBehavior", LeashedPawnMapExitBehavior.StayInPlace);
            Clamp();
        }

        public void Clamp()
        {
            LeadYourPetRules.ClampPetControlSettings(
                ref minPetAgeYears,
                ref maxPetAgeYears,
                ref ordinaryTravelRatkinYoungCount,
                ref maxLeashLength,
                ref maxMouseEggPetLeashStartDistance);
        }
    }
}
