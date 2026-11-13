using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProgressionLESSStorage
{
    // Perspective Shift's storage menu: Keep, Hold, Equip, Wear and closing the menu with an item start a timed retrieval when the storage has a retrieval time (inert without Perspective Shift)
    [StaticConstructorOnStartup]
    public static class PerspectiveShiftCompat
    {
        private sealed class Retrieval
        {
            public int startTick;
            public int duration;
            public Action finish = null!;
            public string title = "";
            public string text = "";
        }

        // Lifetime: the open storage menu window; entries go when the window is collected
        private static readonly ConditionalWeakTable<Window, Retrieval> active = new ConditionalWeakTable<Window, Retrieval>();

        // Lifetime: the open storage menu window; the cursor item whose retrieval time has been waited out
        private static readonly ConditionalWeakTable<Window, Thing> paid = new ConditionalWeakTable<Window, Thing>();

        private static FieldInfo? storageField;
        private static FieldInfo? cursorField;
        private static FieldInfo? avatarField;
        private static FieldInfo? avatarPawnField;
        private static Type? menuType;
        private static bool closing;
        private static string keepLabel = "";
        private static string holdLabel = "";
        private static string equipLabel = "";
        private static string wearLabel = "";

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

            menuType = menu;
            keepLabel = "PS_Keep".Translate();
            holdLabel = "PS_Hold".Translate();
            equipLabel = "PS_Equip".Translate();
            wearLabel = "PS_Wear".Translate();
            var harmony = new Harmony("cruesoe.progressionlessstorage.perspectiveshift");
            harmony.Patch(draw, prefix: new HarmonyMethod(typeof(PerspectiveShiftCompat), nameof(DrawActionButtonPrefix)));
            harmony.Patch(contents, prefix: new HarmonyMethod(typeof(PerspectiveShiftCompat), nameof(DoWindowContentsPrefix)));
            MethodInfo? tryRemove = AccessTools.Method(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) });
            if (tryRemove != null)
            {
                harmony.Patch(tryRemove, prefix: new HarmonyMethod(typeof(PerspectiveShiftCompat), nameof(TryRemovePrefix)));
            }
        }

        private static bool RetrievalApplies(Thing storage)
        {
            return ProgressionLESSStorageMod.Settings?.disableRetrievalTime != true && storage.def.GetStatValueAbstract(StorageStatDefOf.Ferny_RetrievalTime) > 0f;
        }

        // Starts the timed retrieval; false when there is nothing to wait for
        private static bool StartRetrieval(Window window, Action finish)
        {
            if (active.TryGetValue(window, out _))
            {
                return true;
            }

            if (!(storageField!.GetValue(window) is Thing storage) || !RetrievalApplies(storage))
            {
                return false;
            }

            float seconds = RetrievalUtility.Seconds((storage as ISlotGroupParent)?.GetSlotGroup(), AvatarPawn());
            if (seconds <= 0f)
            {
                return false;
            }

            paid.Remove(window);
            var cursor = cursorField!.GetValue(window) as Thing;
            if (cursor != null)
            {
                paid.Add(window, cursor);
            }

            active.Add(window, new Retrieval
            {
                startTick = Find.TickManager.TicksGame,
                duration = (int)(seconds * GenTicks.TicksPerRealSecond),
                finish = finish,
                title = storage.LabelNoParenthesisCap,
                text = "Ferny_Retrieving".Translate(cursor?.LabelCap ?? "")
            });
            window.forcePause = false;
            return true;
        }

        // Closing the menu (Esc, clicking off, the X) drops the cursor item at the avatar's feet, so an item not yet waited for runs the retrieval first
        public static bool TryRemovePrefix(Window window, bool doCloseSound)
        {
            if (closing || window == null || window.GetType() != menuType)
            {
                return true;
            }

            if (active.TryGetValue(window, out _))
            {
                return false;
            }

            if (!(cursorField!.GetValue(window) is Thing cursor) || cursor.Destroyed)
            {
                return true;
            }

            if (paid.TryGetValue(window, out Thing? paidItem) && paidItem == cursor)
            {
                return true;
            }

            return !StartRetrieval(window, () =>
            {
                closing = true;
                try
                {
                    Find.WindowStack.TryRemove(window, doCloseSound);
                }
                finally
                {
                    closing = false;
                }
            });
        }

        private static Pawn? AvatarPawn()
        {
            object? avatar = avatarField?.GetValue(null);
            return avatar == null ? null : avatarPawnField?.GetValue(avatar) as Pawn;
        }

        public static void DrawActionButtonPrefix(Window __instance, string label, ref Action<int> onClick)
        {
            if (label != keepLabel && label != holdLabel && label != equipLabel && label != wearLabel || !(storageField!.GetValue(__instance) is Thing storage))
            {
                return;
            }

            if (!RetrievalApplies(storage))
            {
                return;
            }

            Action<int> original = onClick;
            onClick = button =>
            {
                if (!StartRetrieval(__instance, () => original(button)))
                {
                    original(button);
                }
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

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), retrieval.title);
            Text.Font = GameFont.Small;

            var barRect = new Rect(inRect.x + 20f, inRect.center.y - 15f, inRect.width - 40f, 30f);
            Widgets.FillableBar(barRect, Mathf.Clamp01((float)elapsed / retrieval.duration));
            Text.Anchor = TextAnchor.LowerCenter;
            Widgets.Label(new Rect(barRect.x, barRect.y - 30f, barRect.width, 28f), retrieval.text);
            Text.Anchor = TextAnchor.UpperLeft;
            return false;
        }
    }
}
