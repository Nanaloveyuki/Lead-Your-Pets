using UnityEngine;
using Verse;

namespace LeadYourPet
{
    public class LeadYourPetMod : Mod
    {
        public static LeadYourPetSettings Settings;

        public LeadYourPetMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LeadYourPetSettings>();
        }

        public override string SettingsCategory()
        {
            return "牵着你的宠物 Continued";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            bool infinite = Settings.infiniteLeash;
            listing.CheckboxLabeled("拴绳无限长度", ref infinite, "开启后忽略最大拴绳长度。");
            Settings.infiniteLeash = infinite;

            bool showInteractionText = Settings.showInteractionText;
            listing.CheckboxLabeled("显示头顶浮字", ref showInteractionText, "关闭后不再显示互动时的头顶文字。");
            Settings.showInteractionText = showInteractionText;

            if (!Settings.infiniteLeash)
            {
                listing.Label($"拴绳最高长度: {Settings.maxLeashLength}");
                Settings.maxLeashLength = Mathf.RoundToInt(listing.Slider(Settings.maxLeashLength, 3f, 30f));
            }
            else
            {
                listing.Label("拴绳最高长度: 无限");
            }

            listing.Label($"{"LeadYourPet_Settings_MouseEggLeashStartDistance".Translate().Resolve()} {Settings.maxMouseEggPetLeashStartDistance}");
            Settings.maxMouseEggPetLeashStartDistance = Mathf.RoundToInt(listing.Slider(Settings.maxMouseEggPetLeashStartDistance, 1f, 30f));

            listing.End();
            Settings.Write();
        }
    }
}
