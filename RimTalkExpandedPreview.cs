using HarmonyLib;
using Verse;
using RimWorld;

namespace RimTalk.CommonKnowledgeEnhance
{
    /// <summary>
    /// RimTalk-ExpandMemory 常识库增强预览版
    /// 功能：
    /// 1. 使用新的标签匹配逻辑（类似世界书）
    /// 2. 常识触发常识（多轮匹配）
    /// 3. UI增强：允许设置常识是否可被提取内容、是否可被匹配
    /// </summary>
    public class RimTalkCommonKnowledgeEnhance : Mod
    {
        public static RimTalkCommonKnowledgeEnhanceSettings Settings;
        public static Harmony HarmonyInstance;

        public RimTalkCommonKnowledgeEnhance(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimTalkCommonKnowledgeEnhanceSettings>();
            
            // 初始化Harmony
            HarmonyInstance = new Harmony("RimTalk.CommonKnowledgeEnhance");
            HarmonyInstance.PatchAll();
            
            Log.Message("[RimTalk-CommonKnowledgeEnhance] Mod initialized with Harmony patches applied.");
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimTalk Common Knowledge Enhance";
        }
    }

    /// <summary>
    /// Mod设置
    /// </summary>
    public class RimTalkCommonKnowledgeEnhanceSettings : ModSettings
    {
        // 是否启用新的标签匹配逻辑
        public bool useNewTagMatching = true;
        
        // 是否启用常识触发常识
        public bool enableKnowledgeChaining = true;
        
        // 常识链最大轮数（默认2轮）
        public int maxChainingRounds = 2;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref useNewTagMatching, "useNewTagMatching", true);
            Scribe_Values.Look(ref enableKnowledgeChaining, "enableKnowledgeChaining", true);
            Scribe_Values.Look(ref maxChainingRounds, "maxChainingRounds", 2);
        }

        public void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // 标题
            Text.Font = GameFont.Medium;
            listingStandard.Label("RimTalkEP_SettingsTitle".Translate());
            Text.Font = GameFont.Small;
            listingStandard.Gap();

            // 新标签匹配逻辑
            listingStandard.CheckboxLabeled(
                "RimTalkEP_UseNewTagMatching".Translate(), 
                ref useNewTagMatching,
                "RimTalkEP_UseNewTagMatchingDesc".Translate()
            );
            listingStandard.Gap();

            // 常识触发常识
            listingStandard.CheckboxLabeled(
                "RimTalkEP_EnableKnowledgeChaining".Translate(), 
                ref enableKnowledgeChaining,
                "RimTalkEP_EnableKnowledgeChainingDesc".Translate()
            );
            listingStandard.Gap();

            // 最大轮数
            if (enableKnowledgeChaining)
            {
                listingStandard.Label("RimTalkEP_MaxChainingRounds".Translate(maxChainingRounds));
                maxChainingRounds = (int)listingStandard.Slider(maxChainingRounds, 1, 5);
                listingStandard.Gap();
            }

            // 重置按钮
            if (listingStandard.ButtonText("RimTalkEP_ResetToDefaults".Translate()))
            {
                useNewTagMatching = true;
                enableKnowledgeChaining = true;
                maxChainingRounds = 2;
            }

            listingStandard.End();
        }
    }
}
