// 文件功能：RimTalk扩展预览模组的主类。
using HarmonyLib;
using Verse;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

namespace RimTalk_ExpandedPreview
{
    public class RimTalk_ExpandedPreviewMod : Mod
    {
        public static CustomKeywordUI Settings;
        private Vector2 scrollPosition = Vector2.zero;
        private static bool keywordManagementExpanded = false;
        private static bool experimentalKeywordExpanded = false;

        public RimTalk_ExpandedPreviewMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CustomKeywordUI>();

            var harmony = new Harmony("MEKP.RimTalkKnowledgePreview");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[RimTalk_ExpandedPreview] Mod loaded and Harmony patches applied.");
        }

        public override string SettingsCategory() => "RTExpPrev_ModSettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, 1200f);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

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

            listing.Gap(24f);

            DrawCollapsibleSection(listing, "RTExpPrev_Settings_KeywordManagementSection".Translate(), ref keywordManagementExpanded, () => DrawKeywordManagementSettings(listing));
            DrawCollapsibleSection(listing, "RTExpPrev_Settings_ExperimentalKeywordSection".Translate(), ref experimentalKeywordExpanded, () => DrawExperimentalKeywordSettings(listing));

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawCollapsibleSection(Listing_Standard listing, string title, ref bool expanded, System.Action drawContent)
        {
            Rect headerRect = listing.GetRect(35f);
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            Text.Font = GameFont.Medium;
            Rect labelRect = new Rect(headerRect.x + 35f, headerRect.y + 5f, headerRect.width - 35f, headerRect.height);
            Widgets.Label(labelRect, title);
            Text.Font = GameFont.Small;
            
            Rect iconRect = new Rect(headerRect.x + 8f, headerRect.y + 8f, 22f, 22f);
            if (Widgets.ButtonImage(iconRect, expanded ? TexButton.Collapse : TexButton.Reveal))
            {
                expanded = !expanded;
            }
            
            listing.Gap(3f);
            
            if (expanded)
            {
                listing.Gap(3f);
                drawContent?.Invoke();
                listing.Gap(6f);
            }
            
            listing.GapLine();
        }

        private void DrawKeywordManagementSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RTExpPrev_Settings_EnableKnowledgeCycle".Translate(), ref Settings.enableKnowledgeCycle);
            if (Settings.enableKnowledgeCycle)
            {
                listing.Label("RTExpPrev_Settings_KnowledgeCycleCount".Translate(Settings.knowledgeExtractionCycles));
                Settings.knowledgeExtractionCycles = (int)listing.Slider(Settings.knowledgeExtractionCycles, 1, 5);
                GUI.color = Color.gray;
                listing.Label("RTExpPrev_Settings_KnowledgeCycleExplanation".Translate());
                GUI.color = Color.white;
            }
            listing.Gap(12f);

            listing.Label("RTExpPrev_MaxExtractableKnowledgeLimit".Translate(Settings.maxExtractableKnowledge));
            Settings.maxExtractableKnowledge = (int)listing.Slider(Settings.maxExtractableKnowledge, 1, 10);
            GUI.color = Color.gray;
            listing.Label("RTExpPrev_MaxExtractableKnowledgeLimit_Desc".Translate());
            GUI.color = Color.white;
            listing.Gap(12f);

            listing.Label("RTExpPrev_Settings_KnowledgeContentKeywordLimit".Translate(Settings.knowledgeContentKeywordLimit));
            Settings.knowledgeContentKeywordLimit = (int)listing.Slider(Settings.knowledgeContentKeywordLimit, 1, 50);
            GUI.color = Color.gray;
            listing.Label("RTExpPrev_Settings_KnowledgeContentKeywordLimit_Desc".Translate());
            GUI.color = Color.white;
            listing.Gap(12f);

            listing.Label("RTExpPrev_Settings_ExtractedKnowledgeBonus".Translate(Settings.extractedContentKnowledgeBonus.ToString("F2")));
            Settings.extractedContentKnowledgeBonus = listing.Slider(Settings.extractedContentKnowledgeBonus, 0f, 2f);
            GUI.color = Color.gray;
            listing.Label("RTExpPrev_Settings_ExtractedKnowledgeBonus_Desc".Translate());
            GUI.color = Color.white;
        }

        private void DrawExperimentalKeywordSettings(Listing_Standard listing)
        {
            Rect firstRowRect = listing.GetRect(30f);
            Rect checkboxRect = new Rect(firstRowRect.x, firstRowRect.y, firstRowRect.width * 0.6f, firstRowRect.height);
            Rect buttonRect = new Rect(checkboxRect.xMax + 10f, firstRowRect.y, firstRowRect.width * 0.4f - 10f, firstRowRect.height);

            Widgets.CheckboxLabeled(checkboxRect, "RTExpPrev_Settings_EnableNewKeywordLogic".Translate(), ref Settings.useNewKeywordLogic);
            if (Widgets.ButtonText(buttonRect, "RTExpPrev_Settings_ManageStopWordsButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_StopWords());
            }
            listing.Gap(12f);
            
            // 两列布局
            Rect columnRect = listing.GetRect(280f);
            float columnWidth = (columnRect.width - 20f) / 2f;
            
            // 左列：中文关键词评分
            Listing_Standard leftColumn = new Listing_Standard();
            Rect leftRect = new Rect(columnRect.x, columnRect.y, columnWidth, columnRect.height);
            leftColumn.Begin(leftRect);
            
            leftColumn.Label("RTExpPrev_Settings_ChineseKeywordScoring".Translate());
            leftColumn.Label("RTExpPrev_Settings_ChineseLength2".Translate(Settings.chineseLength2Score.ToString("F1")));
            Settings.chineseLength2Score = leftColumn.Slider(Settings.chineseLength2Score, 0f, 20f);
            leftColumn.Label("RTExpPrev_Settings_ChineseLength3".Translate(Settings.chineseLength3Score.ToString("F1")));
            Settings.chineseLength3Score = leftColumn.Slider(Settings.chineseLength3Score, 0f, 20f);
            leftColumn.Label("RTExpPrev_Settings_ChineseLength4".Translate(Settings.chineseLength4Score.ToString("F1")));
            Settings.chineseLength4Score = leftColumn.Slider(Settings.chineseLength4Score, 0f, 20f);
            leftColumn.Label("RTExpPrev_Settings_ChineseLength5".Translate(Settings.chineseLength5Score.ToString("F1")));
            Settings.chineseLength5Score = leftColumn.Slider(Settings.chineseLength5Score, 0f, 20f);
            leftColumn.Label("RTExpPrev_Settings_ChineseLength6".Translate(Settings.chineseLength6Score.ToString("F1")));
            Settings.chineseLength6Score = leftColumn.Slider(Settings.chineseLength6Score, 0f, 20f);
            
            leftColumn.End();
            
            // 右列：英文关键词评分
            Listing_Standard rightColumn = new Listing_Standard();
            Rect rightRect = new Rect(columnRect.x + columnWidth + 20f, columnRect.y, columnWidth, columnRect.height);
            rightColumn.Begin(rightRect);
            
            rightColumn.Label("RTExpPrev_Settings_EnglishKeywordScoring".Translate());
            rightColumn.Label("RTExpPrev_Settings_EnglishBaseScore".Translate(Settings.englishBaseScore.ToString("F1")));
            Settings.englishBaseScore = rightColumn.Slider(Settings.englishBaseScore, 0f, 20f);
            rightColumn.Label("RTExpPrev_Settings_EnglishIncrementScore".Translate(Settings.englishIncrementScore.ToString("F1")));
            Settings.englishIncrementScore = rightColumn.Slider(Settings.englishIncrementScore, 0f, 10f);
            rightColumn.Label("RTExpPrev_Settings_EnglishMaxScore".Translate(Settings.englishMaxScore.ToString("F1")));
            Settings.englishMaxScore = rightColumn.Slider(Settings.englishMaxScore, 0f, 50f);
            
            rightColumn.End();
            
            listing.Gap(12f);
            
            // 凝固度加分（全宽）
            listing.Label("RTExpPrev_Settings_CohesionBonus".Translate());
            listing.Label("RTExpPrev_Settings_CohesionBonusPerMatch".Translate(Settings.cohesionBonusPerMatch.ToString("F1")));
            Settings.cohesionBonusPerMatch = listing.Slider(Settings.cohesionBonusPerMatch, 0f, 10f);
            listing.Label("RTExpPrev_Settings_CohesionMaxBonus".Translate(Settings.cohesionMaxBonus.ToString("F1")));
            Settings.cohesionMaxBonus = listing.Slider(Settings.cohesionMaxBonus, 0f, 20f);
        }
    }
}
