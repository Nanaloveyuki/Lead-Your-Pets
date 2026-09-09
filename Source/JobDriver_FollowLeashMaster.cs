using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public class JobDriver_FollowLeashMaster : JobDriver
    {
        private Pawn Master => job.GetTarget(TargetIndex.A).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override string GetReport()
        {
            return base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            Toil toil = ToilMaker.MakeToil("LeadYourPet_FollowLeashMaster");
            toil.tickIntervalAction = delegate
            {
                Pawn master = Master;
                if (master == null || LeadYourPetUtility.Component == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                LeashLink link = LeadYourPetUtility.Component.GetLinkForPet(pawn);
                if (link == null || link.Master != master)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                if (!pawn.CanReach(master, PathEndMode.Touch, Danger.Deadly))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!pawn.pather.Moving || pawn.pather.Destination != master)
                {
                    pawn.pather.StartPath(master, PathEndMode.Touch);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
        }
    }
}
