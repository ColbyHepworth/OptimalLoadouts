using System;
using HarmonyLib;
using OptimalLoadouts.ApparelOrders.Patches;
using Verse;

namespace OptimalLoadouts
{
    /// <summary>Applies the mod's Harmony patches when the game loads mods.</summary>
    public class OptimalLoadoutsMod : Mod
    {
        public OptimalLoadoutsMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("groovy.optimalloadouts");
            TryPatch(harmony, typeof(ApparelScoreGainPatch), "pawns pick apparel the usual way, ignoring stat orders");
            TryPatch(harmony, typeof(ApparelPolicyCopyFromPatch), "duplicated apparel policies start without a stat order");
            TryPatch(harmony, typeof(ManageApparelPoliciesPatch), "Manage apparel policies has no outfit preview");
        }

        private static void TryPatch(Harmony harmony, Type patch, string whatHappensInstead)
        {
            try
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
            catch (Exception e)
            {
                Log.Warning($"[Optimal Loadouts] Couldn't hook the game, so {whatHappensInstead}: " + e);
            }
        }
    }
}
