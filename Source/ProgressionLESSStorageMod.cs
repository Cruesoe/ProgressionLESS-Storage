using HarmonyLib;
using UnityEngine;
using Verse;

namespace ProgressionLESSStorage
{
    public class ProgressionLESSStorageMod : Mod
    {
        public static ProgressionLESSStorageSettings? Settings;

        public ProgressionLESSStorageMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ProgressionLESSStorageSettings>();
            new Harmony("cruesoe.progressionlessstorage").PatchAll();
        }

        public override string SettingsCategory() => "ProgressionLESS: Storage";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("Ferny_DisableRetrievalTime".Translate(), ref Settings!.disableRetrievalTime, "Ferny_DisableRetrievalTimeDesc".Translate());
            list.End();
        }
    }
}
