using Verse;

namespace ProgressionLESSStorage
{
    public class ProgressionLESSStorageSettings : ModSettings
    {
        public bool disableRetrievalTime;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref disableRetrievalTime, "disableRetrievalTime", false);
        }
    }
}
