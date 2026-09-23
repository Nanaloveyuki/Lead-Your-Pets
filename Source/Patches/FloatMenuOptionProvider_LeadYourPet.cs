using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public class FloatMenuOptionProvider_LeadYourPet : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            return LeadYourPetUtility.CanPlayerCommand(context.FirstSelectedPawn);
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || clickedPawn == null)
            {
                yield break;
            }

            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component == null)
            {
                yield break;
            }

            if (LeadYourPetUtility.IsEligibleAnimalPet(clickedPawn))
            {
                foreach (FloatMenuOption option in AnimalOptions(actor, clickedPawn, component))
                {
                    yield return option;
                }
            }

            if (LeadYourPetUtility.IsMouseEgg(clickedPawn))
            {
                foreach (FloatMenuOption option in MouseEggOptions(actor, clickedPawn, component))
                {
                    yield return option;
                }
            }
        }

        private IEnumerable<FloatMenuOption> AnimalOptions(Pawn actor, Pawn pet, LeadYourPetGameComponent component)
        {
            LeashLink current = component.GetLinkForPet(pet);
            if (current != null && current.Master == actor)
            {
                yield return new FloatMenuOption(T("LeadYourPet_StopLeash"), delegate
                {
                    component.EndLeashForPet(pet);
                }, MenuOptionPriority.Default);
            }

            if (!actor.CanReach(pet, PathEndMode.Touch, Danger.Deadly))
            {
                yield return DisabledOption("LeadYourPet_LeadPet", "LeadYourPet_Reason_Unreachable");
                yield break;
            }

            if (current == null || current.Master != actor)
            {
                yield return new FloatMenuOption(T("LeadYourPet_LeadPet"), delegate
                {
                    component.StartLeash(actor, pet, false);
                }, MenuOptionPriority.InitiateSocial);
            }
        }

        private IEnumerable<FloatMenuOption> MouseEggOptions(Pawn actor, Pawn pet, LeadYourPetGameComponent component)
        {
            MouseEggState state = component.GetMouseEggState(pet);
            LeashLink current = component.GetLinkForPet(pet);
            bool ownedByActor = (state != null && state.CurrentMaster == actor) || (current != null && current.Master == actor && current.Kind == LeashLinkKind.MouseEggPet);
            bool canBePet = LeadYourPetUtility.CanBePetMouseEgg(pet, out string petBlockReasonKey);
            bool colonistChild = LeadYourPetUtility.IsColonistMouseEgg(pet);
            string stopLabel = colonistChild ? "不再看着孩子" : T("LeadYourPet_StopMouseEggLeash");
            string twoWayLabel = colonistChild ? "陪孩子玩..." : T("LeadYourPet_MouseEggTwoWay");
            string startLabel = colonistChild ? "看着孩子并牵着" : T("LeadYourPet_SetAsPetMouseEggAndLeash");

            if (ownedByActor)
            {
                yield return new FloatMenuOption(stopLabel, delegate
                {
                    component.ClearMouseEggPetState(pet);
                }, MenuOptionPriority.Default);

                if (!canBePet)
                {
                    yield return colonistChild ? DisabledOptionRaw("不能继续看着孩子", T(petBlockReasonKey)) : DisabledOption("LeadYourPet_MouseEggNoLongerPettable", petBlockReasonKey);
                    yield break;
                }

                bool bidirectionalBlocked = LeadYourPetUtility.ShouldBlockColonistBidirectionalInteraction(actor, pet, LeadYourPetUtility.BidirectionalInteractions.First());
                if (LeadYourPetRules.ShouldOfferManualMouseEggInteractionMenu(ownedByActor, true, bidirectionalBlocked))
                {
                    yield return BuildInteractionMenu(twoWayLabel, actor, pet, component, LeadYourPetUtility.BidirectionalInteractions, true);
                }
                yield break;
            }

            if (!canBePet)
            {
                yield return colonistChild ? DisabledOptionRaw(startLabel, T(petBlockReasonKey)) : DisabledOption("LeadYourPet_SetAsPetMouseEggAndLeash", petBlockReasonKey);
                yield break;
            }

            if (!LeadYourPetUtility.CanStartPlayerPetLeash(actor, pet, true, out string leashReasonKey))
            {
                yield return colonistChild ? DisabledOptionRaw(startLabel, T(leashReasonKey)) : DisabledOption("LeadYourPet_SetAsPetMouseEggAndLeash", leashReasonKey);
                yield break;
            }

            if (!actor.CanReach(pet, PathEndMode.Touch, Danger.Deadly))
            {
                yield return colonistChild ? DisabledOptionRaw(startLabel, T("LeadYourPet_Reason_Unreachable")) : DisabledOption("LeadYourPet_SetAsPetMouseEggAndLeash", "LeadYourPet_Reason_Unreachable");
                yield break;
            }

            yield return new FloatMenuOption(startLabel, delegate
            {
                component.StartLeash(actor, pet, true);
            }, MenuOptionPriority.InitiateSocial);
        }

        private FloatMenuOption BuildInteractionMenu(string label, Pawn actor, Pawn pet, LeadYourPetGameComponent component, IEnumerable<LeadYourPetInteractionKind> kinds, bool rawLabel = false)
        {
            if (!actor.CanReach(pet, PathEndMode.Touch, Danger.Deadly))
            {
                return rawLabel ? DisabledOptionRaw(label, T("LeadYourPet_Reason_Unreachable")) : DisabledOption(label, "LeadYourPet_Reason_Unreachable", true);
            }

            return new FloatMenuOption(rawLabel ? label : T(label), delegate
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (LeadYourPetInteractionKind kind in kinds)
                {
                    LeadYourPetInteractionKind localKind = kind;
                    if (!LeadYourPetUtility.CanTriggerInteraction(actor, pet, localKind, out string partReasonKey))
                    {
                        if (!partReasonKey.NullOrEmpty())
                        {
                            options.Add(DisabledOption(LeadYourPetUtility.InteractionLabel(pet, localKind), partReasonKey, true));
                        }

                        continue;
                    }

                    options.Add(new FloatMenuOption(LeadYourPetUtility.InteractionLabel(pet, localKind), delegate
                    {
                        component.TriggerManualInteraction(actor, pet, localKind);
                    }, MenuOptionPriority.Low));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }, MenuOptionPriority.Low);
        }

        private static FloatMenuOption DisabledOption(string labelKey, string reasonKey)
        {
            return new FloatMenuOption(F("LeadYourPet_LabelWithReason", T(labelKey), T(reasonKey)), null);
        }

        private static FloatMenuOption DisabledOption(string label, string reasonKey, bool rawLabel = true)
        {
            return new FloatMenuOption(F("LeadYourPet_LabelWithReason", label, T(reasonKey)), null);
        }

        private static FloatMenuOption DisabledOptionRaw(string label, string reason)
        {
            return new FloatMenuOption(string.Format(T("LeadYourPet_LabelWithReason"), label, reason), null);
        }

        private static string T(string key)
        {
            return key.Translate().Resolve();
        }

        private static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }
    }
}
