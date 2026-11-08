using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProgressionLESSStorage
{
    // Perspective Shift's storage menu: Keep and Hold start a timed retrieval when the storage has a retrieval time (inert without Perspective Shift)
    [StaticConstructorOnStartup]
    public static class PerspectiveShiftCompat
    {
        private sealed class Retrieval
        {
            public int startTick;
            public int duration;
            public Action finish = null!;
        }

        // Lifetime: the open storage menu window; entries go when the window is collected
        private static readonly ConditionalWeakTable<Window, Retrieval> active = new ConditionalWeakTable<Window, Retrieval>();

        private static FieldInfo? storageField;
        private static FieldInfo? cursorField;
        private static FieldInfo? avatarField;
        private static FieldInfo? avatarPawnField;
        private static string keepLabel = "";
        private static string holdLabel = "";

        static PerspectiveShiftCompat()
        {
            Type? menu = AccessTools.TypeByName("PerspectiveShift.Dialog_StorageMenu");
            if (menu == null)
            {
                return;
            }

            storageField = AccessTools.Field(menu, "storage");
            cursorField = AccessTools.Field(menu, "cursorItem");
            avatarField = AccessTools.Field(AccessTools.TypeByName("PerspectiveShift.State"), "Avatar");
            avatarPawnField = avatarField == null ? null : AccessTools.Field(avatarField.FieldType, "pawn");
            MethodInfo? draw = AccessTools.Method(menu, "DrawActionButton");
            MethodInfo? contents = AccessTools.Method(menu, "DoWindowContents");
            if (storageField == null || cursorField == null || draw == null || contents == null)
            {
                Log.Warning("[ProgressionLESS: Storage] Perspective Shift's storage menu has changed; retrieval time doesn't apply to Keep and Hold.");
                return;
            }

            keepLabel = "PS_Keep".Translate();
            holdLabel = "PS_Hold".Translate();
            var harmony = new Harmony("cruesoe.progressionlessstorage.perspectiveshift");
            harmony.Patch(draw, prefix: new HarmonyMethod(typeof(PerspectiveShiftCompat), nameof(DrawActionButtonPrefix)));
            harmony.Patch(contents, prefix: new HarmonyMethod(typeof(PerspectiveShiftCompat), nameof(DoWindowContentsPrefix)));
        }

        private static Pawn? AvatarPawn()
        {
            object? avatar = avatarField?.GetValue(null);
            return avatar == null ? null : avatarPawnField?.GetValue(avatar) as Pawn;
        }

        public static void DrawActionButtonPrefix(Window __instance, string label, ref Action<int> onClick)
        {
            if (label != keepLabel && label != holdLabel || !(storageField!.GetValue(__instance) is Thing storage))
            {
                return;
            }

            if (ProgressionLESSStorageMod.Settings?.disableRetrievalTime == true || storage.def.GetStatValueAbstract(StorageStatDefOf.Ferny_RetrievalTime) <= 0f)
            {
                return;
            }

            Action<int> original = onClick;
            onClick = button =>
            {
                if (active.TryGetValue(__instance, out _))
                {
                    return;
                }

                float seconds = RetrievalUtility.Seconds((storage as ISlotGroupParent)?.GetSlotGroup(), AvatarPawn());
                if (seconds <= 0f)
                {
                    original(button);
                    return;
                }

                active.Add(__instance, new Retrieval
                {
                    startTick = Find.TickManager.TicksGame,
                    duration = (int)(seconds * GenTicks.TicksPerRealSecond),
                    finish = () => original(button)
                });
                __instance.forcePause = false;
            };
        }

        public static bool DoWindowContentsPrefix(Window __instance, Rect inRect)
        {
            if (!active.TryGetValue(__instance, out Retrieval? retrieval) || retrieval == null)
            {
                return true;
            }

            int elapsed = Find.TickManager.TicksGame - retrieval.startTick;
            if (elapsed >= retrieval.duration)
            {
                active.Remove(__instance);
                __instance.forcePause = true;
                retrieval.finish();
                return false;
            }

            var item = cursorField!.GetValue(__instance) as Thing;
            var storage = storageField!.GetValue(__instance) as Thing;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), storage?.LabelNoParenthesisCap ?? "");
            Text.Font = GameFont.Small;

            var barRect = new Rect(inRect.x + 20f, inRect.center.y - 15f, inRect.width - 40f, 30f);
            Widgets.FillableBar(barRect, Mathf.Clamp01((float)elapsed / retrieval.duration));
            Text.Anchor = TextAnchor.LowerCenter;
            Widgets.Label(new Rect(barRect.x, barRect.y - 30f, barRect.width, 28f), "Ferny_Retrieving".Translate(item?.LabelCap ?? ""));
            Text.Anchor = TextAnchor.UpperLeft;
            return false;
        }
    }
}
