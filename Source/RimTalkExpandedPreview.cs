using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace RimTalk.CommonKnowledgeEnhance
{
    /// <summary>
    /// RimTalk-ExpandMemory 常识库增强预览版
    /// 功能：
    /// 1. 使用新的标签匹配逻辑（类似世界书）
    /// 2. 常识触发常识（多轮匹配）
    /// 3. UI增强：允许设置常识是否可被提取内容、是否可被匹配
    /// 4. ONNX 向量检索增强（新增）
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
            
            Log.Message("RimTalkEP_ModInitialized".Translate());
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimTalkEP_SettingsCategory".Translate();
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
        
        // ⭐ 向量增强设置
        public bool enableVectorEnhancement = false;  // 是否启用向量补充（默认关闭）
        public float vectorSimilarityThreshold = 0.75f;  // 向量相似度阈值（0-1）
        public int maxVectorResults = 5;  // 最多补充几条向量匹配的常识
        
        // 云端 Embedding 配置
        public string embeddingApiKey = "";
        public string embeddingApiUrl = "https://api.siliconflow.cn/v1/embeddings";
        public string embeddingModel = "BAAI/bge-m3";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref useNewTagMatching, "useNewTagMatching", true);
            Scribe_Values.Look(ref enableKnowledgeChaining, "enableKnowledgeChaining", true);
            Scribe_Values.Look(ref maxChainingRounds, "maxChainingRounds", 2);
            Scribe_Values.Look(ref enableVectorEnhancement, "enableVectorEnhancement", false);
            Scribe_Values.Look(ref vectorSimilarityThreshold, "vectorSimilarityThreshold", 0.75f);
            Scribe_Values.Look(ref maxVectorResults, "maxVectorResults", 5);
            Scribe_Values.Look(ref embeddingApiKey, "embeddingApiKey", "");
            Scribe_Values.Look(ref embeddingApiUrl, "embeddingApiUrl", "https://api.siliconflow.cn/v1/embeddings");
            Scribe_Values.Look(ref embeddingModel, "embeddingModel", "BAAI/bge-m3");
        }

        public void DoSettingsWindowContents(Rect inRect)
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

            // ⭐ 向量增强设置
            listingStandard.CheckboxLabeled(
                "启用向量增强", 
                ref enableVectorEnhancement,
                "使用向量检索补充常识匹配结果（需要配置 Embedding API）"
            );
            listingStandard.Gap();

            if (enableVectorEnhancement)
            {
                // 向量匹配参数
                Text.Font = GameFont.Small;
                GUI.color = new Color(0.8f, 0.9f, 1f);
                listingStandard.Label("【向量匹配参数】");
                GUI.color = Color.white;
                listingStandard.Gap(6f);

                listingStandard.Label($"向量相似度阈值: {vectorSimilarityThreshold:F2}");
                vectorSimilarityThreshold = listingStandard.Slider(vectorSimilarityThreshold, 0.5f, 0.95f);
                listingStandard.Gap();

                listingStandard.Label($"最大向量结果数: {maxVectorResults}");
                maxVectorResults = (int)listingStandard.Slider(maxVectorResults, 1, 10);
                listingStandard.Gap(12f);

                // 云端 Embedding API 配置
                GUI.color = new Color(1f, 0.9f, 0.8f);
                listingStandard.Label("【云端 Embedding API 配置】");
                GUI.color = Color.white;
                listingStandard.Gap(6f);

                // API Key
                listingStandard.Label("API Key:");
                embeddingApiKey = listingStandard.TextEntry(embeddingApiKey);
                if (string.IsNullOrEmpty(embeddingApiKey))
                {
                    GUI.color = Color.yellow;
                    listingStandard.Label("  ⚠️ 未配置 API Key，向量功能将无法使用");
                    GUI.color = Color.white;
                }
                listingStandard.Gap();

                // API URL
                listingStandard.Label("API URL:");
                embeddingApiUrl = listingStandard.TextEntry(embeddingApiUrl);
                listingStandard.Gap();

                // Model
                listingStandard.Label("Embedding Model:");
                embeddingModel = listingStandard.TextEntry(embeddingModel);
                listingStandard.Gap();

                // 说明文字
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                listingStandard.Label("💡 推荐平台：硅基流动");
                listingStandard.Label("   推荐模型: BAAI/bge-m3 (不要钱)");
                listingStandard.Label("   或 Qwen/Qwen3-Embedding-8B (精度更高)");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                listingStandard.Gap(12f);
            }

            // 重置按钮
            if (listingStandard.ButtonText("RimTalkEP_ResetToDefaults".Translate()))
            {
                useNewTagMatching = true;
                enableKnowledgeChaining = true;
                maxChainingRounds = 2;
                enableVectorEnhancement = false;
                vectorSimilarityThreshold = 0.75f;
                maxVectorResults = 5;
                embeddingApiKey = "";
                embeddingApiUrl = "https://api.siliconflow.cn/v1/embeddings";
                embeddingModel = "BAAI/bge-m3";
            }

            listingStandard.End();
        }
    }
}
