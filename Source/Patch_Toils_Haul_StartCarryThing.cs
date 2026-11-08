using System;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace ProgressionLESSStorage
{
    // Delays picking a thing up from storage that has a retrieval time; the toil stays instant when there is no wait
    [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.StartCarryThing))]
    public static class Patch_Toils_Haul_StartCarryThing
    {
        public static void Postfix(Toil __result, TargetIndex haulableInd)
        {
            Toil toil = __result;
            Action? pickUp = toil.initAction;
            Action? originalTick = toil.tickAction;
            bool started = false;
            bool waiting = false;
            int total = 1;

            // Delay mode so a wait restored from a save keeps ticking; initAction switches to instant when there is no wait
            toil.defaultCompleteMode = ToilCompleteMode.Delay;

            toil.initAction = () =>
            {
                started = true;
                Pawn actor = toil.actor;
                int ticks = RetrievalUtility.Ticks(actor.CurJob.GetTarget(haulableInd).Thing, actor);
                if (ticks > 0)
                {
                    // At least 2 ticks: the pickup runs in tickAction on the tick before the toil ends
                    ticks = Math.Max(2, ticks);
                    waiting = true;
                    total = ticks;
                    toil.defaultCompleteMode = ToilCompleteMode.Delay;
                    actor.jobs.curDriver.ticksLeftThisToil = ticks;
                    return;
                }

                waiting = false;
                toil.defaultCompleteMode = ToilCompleteMode.Instant;
                pickUp?.Invoke();
            };

            toil.tickAction = () =>
            {
                JobDriver driver = toil.actor.jobs.curDriver;
                if (!started)
                {
                    // Loaded mid-wait: initAction didn't run this session
                    started = true;
                    waiting = true;
                    total = Math.Max(1, driver.ticksLeftThisToil);
                }

                if (!waiting)
                {
                    originalTick?.Invoke();
                    return;
                }

                Thing thing = toil.actor.CurJob.GetTarget(haulableInd).Thing;
                if (thing == null || !thing.Spawned)
                {
                    waiting = false;
                    toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                if (driver.ticksLeftThisToil <= 1)
                {
                    waiting = false;
                    pickUp?.Invoke();
                }
            };

            toil.WithProgressBar(haulableInd, () =>
            {
                JobDriver? driver = toil.actor?.jobs?.curDriver;
                return waiting && driver != null ? 1f - (float)driver.ticksLeftThisToil / total : 0f;
            });
        }
    }
}
