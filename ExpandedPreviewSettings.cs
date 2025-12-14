// 文件功能：RimTalk扩展预览模组的主类。
using HarmonyLib;
using Verse;
using System.Reflection;
using UnityEngine; // For Rect
using System.Collections.Generic; // For List

namespace RimTalk_ExpandedPreview
{
    // 定义设置类
    public class ExpandedPreviewSettings : ModSettings
    {
        public bool enableKnowledgeCycle = false;
        public int knowledgeExtractionCycles = 2;

        // 新增：常识内容关键词数量限制
        public int knowledgeContentKeywordLimit = 3;

        // 新增：被提取内容常识加分
        public float extractedContentKnowledgeBonus = 0.20f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableKnowledgeCycle, "enableKnowledgeCycle", false);
            Scribe_Values.Look(ref knowledgeExtractionCycles, "knowledgeExtractionCycles", 2);
            
            // 序列化新增设置
            Scribe_Values.Look(ref knowledgeContentKeywordLimit, "knowledgeContentKeywordLimit", 3);
            Scribe_Values.Look(ref extractedContentKnowledgeBonus, "extractedContentKnowledgeBonus", 0.20f);
        }
    }

    public class RimTalk_ExpandedPreviewMod : Mod
    {
        public static ExpandedPreviewSettings Settings;
        public static CustomKeywordUI CustomKeywordSettings;

        public RimTalk_ExpandedPreviewMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ExpandedPreviewSettings>();
            CustomKeywordSettings = GetSettings<CustomKeywordUI>();

            var harmony = new Harmony("MEKP.RimTalkKnowledgePreview");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[RimTalk_ExpandedPreview] Mod loaded and Harmony patches applied.");
        }

        public override string SettingsCategory() => "RTExpPrev_ModSettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("RTExpPrev_Settings_Title".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            GUI.color = Color.gray;
            listing.Label("RTExpPrev_Settings_Description_Short".Translate());
            GUI.color = Color.white;
            listing.Gap(8f);

            // 全局设置按钮
            if (Widgets.ButtonText(listing.GetRect(40f), "RTExpPrev_Settings_GlobalKeywordsButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SettingsUI());
            }

            listing.Gap(6f);

            // 存档设置按钮
            Rect saveRect = listing.GetRect(40f);
            if (Current.Game == null)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.DrawBox(saveRect);
                Widgets.Label(saveRect.ContractedBy(6f), "RTExpPrev_Settings_SaveKeywordsDisabledLabel".Translate());
                GUI.color = Color.white;
            }
            else
            {
                if (Widgets.ButtonText(saveRect, "RTExpPrev_Settings_SaveKeywordsButton".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_SaveGameKeywords());
                }
            }

            listing.Gap(12f);
            GUI.color = new Color(0.7f, 0.9f, 1f);
            listing.Label("RTExpPrev_Settings_Hint".Translate());
            GUI.color = Color.white;

            listing.Gap(24f); // 增加间距

            // --- 新增设置项 ---
            
            // 1. 常识内容关键词数量限制
            listing.Label("RTExpPrev_Settings_KnowledgeContentKeywordLimit".Translate(Settings.knowledgeContentKeywordLimit)); // 新增Key
            Settings.knowledgeContentKeywordLimit = (int)listing.Slider(Settings.knowledgeContentKeywordLimit, 1, 50);
            GUI.color = Color.gray;
            listing.Label("RTExpPrev_Settings_KnowledgeContentKeywordLimit_Desc".Translate()); // 新增Key
            GUI.color = Color.white;
            listing.Gap(12f);

            // 2. 被提取内容常识加分
            listing.Label("RTExpPrev_Settings_ExtractedKnowledgeBonus".Translate(Settings.extractedContentKnowledgeBonus.ToString("F2"))); // 新增Key
            Settings.extractedContentKnowledgeBonus = listing.Slider(Settings.extractedContentKnowledgeBonus, 0f, 2f);
            GUI.color = Color.gray;
            listing.Label("RTExpPrev_Settings_ExtractedKnowledgeBonus_Desc".Translate()); // 新增Key
            GUI.color = Color.white;
            listing.Gap(12f);

            // --- 原有循环次数设置 ---
            listing.CheckboxLabeled("RTExpPrev_Settings_EnableKnowledgeCycle".Translate(), ref Settings.enableKnowledgeCycle);
            if (Settings.enableKnowledgeCycle)
            {
                listing.Label("RTExpPrev_Settings_KnowledgeCycleCount".Translate(Settings.knowledgeExtractionCycles));
                Settings.knowledgeExtractionCycles = (int)listing.Slider(Settings.knowledgeExtractionCycles, 1, 5);
                GUI.color = Color.gray;
                listing.Label("RTExpPrev_Settings_KnowledgeCycleExplanation".Translate());
                GUI.color = Color.white;
            }

            listing.End();
        }
    }
}
