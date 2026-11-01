using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace JusticiarValor
{
    public static class Patch_ImperialCodexInventory
    {
        public const float CarryChance = 0.02f;

        // The new-pawn path runs after titles and inventory have been assigned. Redressing a returning world pawn
        // never enters it, so no per-pawn marker, save component or repeat roll on each visit is needed.
        public static MethodBase TargetMethod() => AccessTools.Method(typeof(PawnGenerator), "GenerateNewPawnInternal");

        public static void Postfix(Pawn __result, ref PawnGenerationRequest request)
        {
            Pawn pawn = __result;
            Faction faction = pawn?.Faction;
            ThingDef codex = MJV_DefOf.MJV_ImperialCodexAuspex;
            if (request.ForceNoGear || pawn == null || pawn.Dead || pawn.kindDef == MJV_DefOf.MJV_Justiciar
                || faction == null || faction.IsPlayer || faction.def.categoryTag != "Empire"
                || pawn.royalty?.GetCurrentTitle(faction) == null || pawn.inventory == null || codex == null)
            {
                return;
            }
            ThingOwner<Thing> inventory = pawn.inventory.innerContainer;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].def == codex)
                {
                    return;
                }
            }
            if (Rand.Chance(CarryChance))
            {
                Thing gift = ThingMaker.MakeThing(codex);
                if (!inventory.TryAdd(gift))
                {
                    gift.Destroy();
                }
            }
        }
    }
}
