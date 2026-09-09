using Verse;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public void AnchorLeashedPetsToThing(Pawn master, Thing anchorThing)
        {
            if (master == null || anchorThing == null)
            {
                return;
            }

            foreach (LeashLink link in GetLinksForMaster(master))
            {
                if (link.Pet == anchorThing || link.Master == anchorThing)
                {
                    continue;
                }

                link.AnchorMode = LeashAnchorMode.Thing;
                link.AnchorThing = anchorThing;
                link.AnchorCell = IntVec3.Invalid;
                link.MasterSleepSuspended = false;
                link.ForceImmediateUpdate = true;
                link.CachedHasValidAutoInteractionPool = false;
                link.Pet.pather?.StopDead();
            }
        }

        public void AnchorLeashedPetsToCell(Pawn master, IntVec3 cell)
        {
            if (master == null || !cell.IsValid || !master.Spawned || master.Map == null || !cell.InBounds(master.Map))
            {
                return;
            }

            foreach (LeashLink link in GetLinksForMaster(master))
            {
                link.AnchorMode = LeashAnchorMode.Cell;
                link.AnchorThing = null;
                link.AnchorCell = cell;
                link.MasterSleepSuspended = false;
                link.ForceImmediateUpdate = true;
                link.CachedHasValidAutoInteractionPool = false;
                link.Pet.pather?.StopDead();
            }
        }

        public void ClearAnchorsForMaster(Pawn master)
        {
            foreach (LeashLink link in GetLinksForMaster(master))
            {
                link.AnchorMode = LeashAnchorMode.None;
                link.AnchorThing = null;
                link.AnchorCell = IntVec3.Invalid;
                link.ForceImmediateUpdate = true;
                link.CachedHasValidAutoInteractionPool = false;
            }
        }
    }
}
