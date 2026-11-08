using RimWorld;

namespace ProgressionLESSStorage
{
    [DefOf]
    public static class StorageStatDefOf
    {
        public static StatDef Ferny_RetrievalTime = null!;

        static StorageStatDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(StorageStatDefOf));
    }
}
