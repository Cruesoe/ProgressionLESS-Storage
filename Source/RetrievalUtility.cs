using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProgressionLESSStorage
{
    public static class RetrievalUtility
    {
        // Reused on the main thread for each lookup; cleared before use
        private static readonly HashSet<ThingDef> distinctDefs = new HashSet<ThingDef>();

        // Ticks a pawn waits before taking the thing out of its storage; 0 when it isn't in storage with a retrieval time
        public static int Ticks(Thing? thing, Pawn? pawn)
        {
            if (thing == null || !thing.Spawned)
            {
                return 0;
            }

            return (int)(Seconds(thing.GetSlotGroup(), pawn) * GenTicks.TicksPerRealSecond);
        }

        // Written retrieval time scaled by variety (different defs / stack capacity) or by fill level, divided by manipulation and half-strength sight
        public static float Seconds(SlotGroup? group, Pawn? pawn)
        {
            if (ProgressionLESSStorageMod.Settings?.disableRetrievalTime == true || !(group?.parent is Thing storage))
            {
                return 0f;
            }

            float written = storage.def.GetStatValueAbstract(StorageStatDefOf.Ferny_RetrievalTime);
            if (written <= 0f)
            {
                return 0f;
            }

            float manipulation = 1f;
            float sight = 1f;
            if (pawn?.health?.capacities != null)
            {
                manipulation = Math.Max(0.1f, pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
                sight = Mathf.Lerp(1f, pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight), 0.5f);
            }

            float speed = manipulation * Math.Max(0.1f, sight);
            int capacity = group.CellsList.Count * Math.Max(1, storage.def.building?.maxItemsInCell ?? 1);
            if (capacity <= 0)
            {
                return written / speed;
            }

            int count = 0;
            if (storage.def.GetModExtension<RetrievalTimeExtension>()?.scaleByUniqueItems == false)
            {
                foreach (Thing _ in group.HeldThings)
                {
                    count++;
                }
            }
            else
            {
                distinctDefs.Clear();
                foreach (Thing held in group.HeldThings)
                {
                    distinctDefs.Add(held.def);
                }

                count = distinctDefs.Count;
                distinctDefs.Clear();
            }

            return written * Math.Min(1f, (float)count / capacity) / speed;
        }
    }
}
