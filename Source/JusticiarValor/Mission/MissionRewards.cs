using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace JusticiarValor
{
    // Vanilla market-value generation samples qualities before checking affordability. If all five samples are too
    // expensive it can permanently exclude a weapon branch until its seed advances, skewing the 95:5 category roll.
    // Keep that generator, with one normal-quality fallback using the game's own stuff, stat and creation APIs.
    public class ThingSetMaker_MJV_Gear : ThingSetMaker_MarketValue
    {
        protected override bool CanGenerateSub(ThingSetMakerParams parms)
        {
            return base.CanGenerateSub(parms) || AffordableNormalEquipment(parms).Any();
        }

        protected override void Generate(ThingSetMakerParams parms, List<Thing> outThings)
        {
            int before = outThings.Count;
            base.Generate(parms, outThings);
            if (outThings.Count == before && AffordableNormalEquipment(parms).TryRandomElement(out ThingStuffPairWithQuality fallback))
            {
                outThings.Add(fallback.MakeThing());
            }
        }

        private IEnumerable<ThingStuffPairWithQuality> AffordableNormalEquipment(ThingSetMakerParams parms)
        {
            float budget = parms.totalMarketValueRange?.max ?? 0f;
            if (budget <= 0f || parms.countRange?.max <= 0) yield break;
            foreach (ThingDef def in AllowedThingDefs(parms))
            {
                if (!def.IsWeapon && !def.IsApparel) continue;
                if (!def.MadeFromStuff)
                {
                    var pair = new ThingStuffPairWithQuality(def, null, QualityCategory.Normal);
                    if (Fits(pair, parms, budget)) yield return pair;
                    continue;
                }
                foreach (ThingDef stuff in GenStuff.AllowedStuffsFor(def, parms.techLevel.GetValueOrDefault()))
                {
                    if (stuff.stuffProps.commonality <= 0f || ThingSetMakerUtility.IsDerpAndDisallowed(def, stuff, parms.qualityGenerator)) continue;
                    var pair = new ThingStuffPairWithQuality(def, stuff, QualityCategory.Normal);
                    if (Fits(pair, parms, budget)) yield return pair;
                }
            }
        }

        private static bool Fits(ThingStuffPairWithQuality pair, ThingSetMakerParams parms, float budget)
            => pair.GetStatValue(StatDefOf.MarketValue) <= budget
                && (!parms.maxTotalMass.HasValue || pair.GetStatValue(StatDefOf.Mass) <= parms.maxTotalMass.Value);
    }

    public static class MissionRewards
    {
        public const string GearTag = "MJV_JusticiarGear";
        public const int BlueprintSermons = 3;
        public const float BlueprintAttendance = 0.75f;
        public const float BlueprintMood = 0.7f;

        // The vanilla maker owns the 95:5 choice, quality/stuff selection and market-value limit. Its filters are
        // resolved once from tags, so rewards do not scan all defs or maintain their own item-generation machinery.
        public static ThingSetMakerParams GearParameters(float budget, Faction empire)
        {
            return new ThingSetMakerParams
            {
                totalMarketValueRange = new FloatRange(budget, budget),
                makingFaction = empire
            };
        }

        public static List<Thing> GenerateGear(float budget, Faction empire)
        {
            ThingSetMaker maker = MJV_DefOf.MJV_Reward_JusticiarGear?.root;
            ThingSetMakerParams parms = GearParameters(budget, empire);
            return budget > 0f && maker != null && maker.CanGenerate(parms) ? maker.Generate(parms) : new List<Thing>();
        }
    }

    // Generates the promised item before acceptance. QuestNode_AddItemsReward handles its preview, ownership,
    // save/load, cleanup and delivery through the game's normal quest reward parts.
    public class QuestNode_MJV_MissionReward : QuestNode
    {
        public SlateRef<bool> blueprintMission;
        public SlateRef<float> gearBudget;

        protected override bool TestRunInt(Slate slate)
        {
            if (blueprintMission.GetValue(slate))
            {
                ResearchProjectDef project = MJV_DefOf.MJV_JusticiarGear;
                return project != null && !project.IsFinished && !project.TechprintRequirementMet && project.Techprint != null;
            }
            float budget = gearBudget.GetValue(slate);
            return budget > 0f && MJV_DefOf.MJV_Reward_JusticiarGear?.root.CanGenerate(
                MissionRewards.GearParameters(budget, slate.Get<Faction>("empire"))) == true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            bool blueprint = blueprintMission.GetValue(slate);
            List<Thing> items = blueprint
                ? new List<Thing> { ThingMaker.MakeThing(MJV_DefOf.MJV_JusticiarGear.Techprint) }
                : MissionRewards.GenerateGear(gearBudget.GetValue(slate), slate.Get<Faction>("empire"));
            slate.Set("missionReward", items);
            slate.Set("missionChallenge", blueprint ? 2 : 1);
            slate.Set("missionAttendance", (blueprint ? MissionRewards.BlueprintAttendance : 0.5f).ToStringPercent());
            slate.Set("missionRewardTerms", blueprint
                ? (string)"MJV_BlueprintMissionTerms".Translate(MissionRewards.BlueprintSermons.Named("SERMONS"),
                    MissionRewards.BlueprintAttendance.ToStringPercent().Named("ATTENDANCE"),
                    MissionRewards.BlueprintMood.ToStringPercent().Named("MOOD"))
                : (string)"MJV_GearMissionTerms".Translate());
        }
    }
}
