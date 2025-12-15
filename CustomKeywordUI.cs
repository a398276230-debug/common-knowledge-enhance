// 文件功能：定义MECP模组的设置。
// File: CustomKeywordUI.cs
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk_ExpandedPreview
{
    public class CustomKeywordUI : ModSettings
    {
        // checkbox textures (lazy-loaded like Patch_PlaySettings)
        private static Texture2D CheckboxOn = null;
        private static Texture2D CheckboxOff = null;

        private static void EnsureCheckboxTexturesLoaded()
        {
            if (CheckboxOn != null && CheckboxOff != null) return;

            string[] onPaths = new[] { "UI/Checkboxes/checkbox_on" };
            string[] offPaths = new[] { "UI/Checkboxes/checkbox_off" };

            foreach (var p in onPaths)
            {
                var t = ContentFinder<Texture2D>.Get(p, false);
                if (t != null)
                {
                    CheckboxOn = t;
                    break;
                }
            }
            foreach (var p in offPaths)
            {
                var t = ContentFinder<Texture2D>.Get(p, false);
                if (t != null)
                {
                    CheckboxOff = t;
                    break;
                }
            }

            // fallbacks to avoid nulls
            if (CheckboxOn == null) CheckboxOn = BaseContent.ClearTex;
            if (CheckboxOff == null) CheckboxOff = BaseContent.ClearTex;
        }

        public List<CustomKeywordEntry> customKeywordEntries = new List<CustomKeywordEntry>();
        public bool enableKnowledgeCycle = false;
        public int knowledgeExtractionCycles = 2;
        
        // ⭐ 从 ExpandedPreviewSettings 合并过来的字段
        public int knowledgeContentKeywordLimit = 3;
        public float extractedContentKnowledgeBonus = 0.20f;
        public int maxExtractableKnowledge = 5; // ⭐ 新增：能提取内容的常识数量上限
        public bool useNewKeywordLogic = false; // (实验性)新关键词提取逻辑
        
        // 新关键词评分参数
        public float chineseLength2Score = 2.0f;
        public float chineseLength3Score = 8.0f;
        public float chineseLength4Score = 10f;
        public float chineseLength5Score = 4.0f;
        public float chineseLength6Score = 6.0f;
        public float englishBaseScore = 4.0f;
        public float englishIncrementScore = 4.0f;
        public float englishMaxScore = 20f;
        public float cohesionBonusPerMatch = 2.0f;
        public float cohesionMaxBonus = 10.0f;
        
        public List<string> startStopWords = new List<string> { "乎", "于", "以", "给", "向", "为", "被", "从", "当", "并", "但", "似乎" };
        public List<string> endStopWords = new List<string> { "附", "处", "中", "内", "上", "下", "间", "前", "后" };

        private Vector2 scrollPosition = Vector2.zero;
        private string searchText = ""; // 新增：搜索文本

        // ⭐ 新增：用于实现拖动功能的变量
        private bool isDraggingCheckbox = false;
        private bool? dragToggleTargetState = null; // true = 开启, false = 关闭

        public override void ExposeData()
        {
            base.ExposeData();
            
            // ⭐ 优先加载旧版字段名（兼容性）
            Scribe_Values.Look(ref enableKnowledgeCycle, "enableKnowledgeCycle", false);
            Scribe_Values.Look(ref knowledgeExtractionCycles, "knowledgeExtractionCycles", 2);
            Scribe_Values.Look(ref knowledgeContentKeywordLimit, "knowledgeContentKeywordLimit", 3);
            Scribe_Values.Look(ref extractedContentKnowledgeBonus, "extractedContentKnowledgeBonus", 0.20f);
            Scribe_Values.Look(ref maxExtractableKnowledge, "maxExtractableKnowledge", 5);
            Scribe_Values.Look(ref useNewKeywordLogic, "useNewKeywordLogic", false);
            
            // 新关键词评分参数
            Scribe_Values.Look(ref chineseLength2Score, "chineseLength2Score", 2.0f);
            Scribe_Values.Look(ref chineseLength3Score, "chineseLength3Score", 8.0f);
            Scribe_Values.Look(ref chineseLength4Score, "chineseLength4Score", 10f);
            Scribe_Values.Look(ref chineseLength5Score, "chineseLength5Score", 4.0f);
            Scribe_Values.Look(ref chineseLength6Score, "chineseLength6Score", 6.0f);
            Scribe_Values.Look(ref englishBaseScore, "englishBaseScore", 4.0f);
            Scribe_Values.Look(ref englishIncrementScore, "englishIncrementScore", 4.0f);
            Scribe_Values.Look(ref englishMaxScore, "englishMaxScore", 20f);
            Scribe_Values.Look(ref cohesionBonusPerMatch, "cohesionBonusPerMatch", 2.0f);
            Scribe_Values.Look(ref cohesionMaxBonus, "cohesionMaxBonus", 10.0f);
            
            Scribe_Collections.Look(ref startStopWords, "startStopWords", LookMode.Value);
            Scribe_Collections.Look(ref endStopWords, "endStopWords", LookMode.Value);

            // 加载关键词列表
            Scribe_Collections.Look(ref customKeywordEntries, "customKeywordEntries", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (customKeywordEntries == null)
                {
                    customKeywordEntries = new List<CustomKeywordEntry>();
                }
                CustomKeywordSettings.Set(customKeywordEntries);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalk_ExpandedPreview] Settings loaded: enableKnowledgeCycle={enableKnowledgeCycle}, cycles={knowledgeExtractionCycles}, bonus={extractedContentKnowledgeBonus}");
                }
            }
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // ... (标题和描述部分代码不变) ...
            Text.Font = GameFont.Medium;
            listing.Label("RTExpPrev_Settings_Title".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(12f);
            GUI.color = Color.gray;
            listing.Label("RTExpPrev_Settings_Description".Translate());
            GUI.color = Color.white;
            listing.Gap(12f);

            // 搜索栏
            Rect searchRect = listing.GetRect(30f);
            GUI.SetNextControlName("KeywordSearch");
            searchText = Widgets.TextField(searchRect, searchText ?? "");
            // 清除按钮
            Rect clearRect = new Rect(searchRect.xMax - 26f, searchRect.y + 3f, 22f, 22f);
            if (Widgets.ButtonImage(clearRect, TexButton.Delete))
            {
                searchText = "";
            }

            listing.Gap(6f);

            // 计算匹配项数量以确定滚动视图高度
            int matchedCount = customKeywordEntries.Count(e => string.IsNullOrEmpty(searchText) || ((e.keyword ?? "").IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0));

            // 使用通用的 KeywordEditorUI 来绘制表格
            Rect tableRect = listing.GetRect(inRect.height - listing.CurHeight - 120f);
            KeywordEditorUI.DrawKeywordTable(tableRect, ref scrollPosition, ref searchText, customKeywordEntries);

            listing.Gap(12f);

            // ... (底部按钮和说明部分代码不变) ...
            Rect buttonRowRect = listing.GetRect(35f);
            float halfWidth = buttonRowRect.width / 2f - 10f;
            Rect addButtonRect = new Rect(buttonRowRect.x, buttonRowRect.y, halfWidth, 35f);
            if (Widgets.ButtonText(addButtonRect, "RTExpPrev_Settings_AddNewKeywordButton".Translate()))
            {
                customKeywordEntries.Add(new CustomKeywordEntry("", true, false, false, false));
            }
            Rect importButtonRect = new Rect(addButtonRect.xMax + 20f, buttonRowRect.y, halfWidth, 35f);
            if (Widgets.ButtonText(importButtonRect, "RTExpPrev_Settings_ImportButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ImportKeywords(AddImportedKeywords));
            }
            listing.Gap(12f);
            GUI.color = new Color(0.7f, 0.9f, 1f);
            listing.Label("RTExpPrev_Settings_ExplanationHeader".Translate());
            listing.Label("RTExpPrev_Settings_ExplanationKeyword".Translate());
            listing.Label("RTExpPrev_Settings_ExplanationWholeWord".Translate());
            listing.Label("RTExpPrev_Settings_ExplanationCaseSensitive".Translate());
            listing.Label("RTExpPrev_Settings_ExplanationNegative".Translate());
            GUI.color = Color.white;


            listing.End();

            if (GUI.changed)
            {
                CustomKeywordSettings.Set(customKeywordEntries);
                Write();
            }
        }

        private void AddImportedKeywords(List<string> keywords)
        {
            // ... (此方法代码不变) ...
            if (keywords == null || !keywords.Any()) return;
            int newKeywordsCount = 0;
            foreach (var keyword in keywords)
            {
                bool exists = customKeywordEntries.Any(e => string.Equals(e.keyword, keyword, System.StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    customKeywordEntries.Add(new CustomKeywordEntry(keyword, true, false, false, false));
                    newKeywordsCount++;
                }
            }
            if (newKeywordsCount > 0)
            {
                CustomKeywordSettings.Set(customKeywordEntries);
                Write();
                Messages.Message("RTExpPrev_Import_SuccessMessage".Translate(newKeywordsCount), MessageTypeDefOf.PositiveEvent, false);
            }
        }

        private float GetViewRectHeight(int count)
        {
            // ... (此方法代码不变) ...
            return 24f + (count * 30f) + 10f;
        }
    }
}
