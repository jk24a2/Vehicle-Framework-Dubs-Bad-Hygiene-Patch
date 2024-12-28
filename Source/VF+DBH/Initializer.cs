using HarmonyLib;
using Verse;

namespace VF_DBH
{
    [StaticConstructorOnStartup]
    internal static class First
    {
        static First()
        {
            Harmony harmony = new Harmony("VF+DBH");
            harmony.PatchAll();
        }
    }
}
