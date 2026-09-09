using Verse;

namespace LeadYourPet
{
    public class LeadYourPetSettings : ModSettings
    {
        public int maxLeashLength = 10;
        public int maxMouseEggPetLeashStartDistance = 10;
        public bool infiniteLeash;
        public bool showInteractionText = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxLeashLength, "maxLeashLength", 10);
            Scribe_Values.Look(ref maxMouseEggPetLeashStartDistance, "maxMouseEggPetLeashStartDistance", 10);
            Scribe_Values.Look(ref infiniteLeash, "infiniteLeash", false);
            Scribe_Values.Look(ref showInteractionText, "showInteractionText", true);
        }
    }
}
