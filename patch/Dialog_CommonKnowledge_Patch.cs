using HarmonyLib;
using RimTalk.Memory;
using RimTalk.Memory.UI;
using System.Collections.Generic;
using System.Linq; // ⭐ 添加 LINQ 命名空间
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace RimTalk_ExpandedPreview
{
    [HarmonyPatch(typeof(Dialog_CommonKnowledge), "DrawEntryList")]
    public static class Dialog_CommonKnowledge_Patch
    {
        // 使用 Prefix 完全重写 DrawEntryList 方法
        [HarmonyPrefix]
        public static bool Prefix(Dialog_CommonKnowledge __instance, Rect rect)
        {
            var library = (CommonKnowledgeLibrary)AccessTools.Field(typeof(Dialog_CommonKnowledge), "library").GetValue(__instance);
            var searchFilter = (string)AccessTools.Field(typeof(Dialog_CommonKnowledge), "searchFilter").GetValue(__instance);
            var scrollPosition = (Vector2)AccessTools.Field(typeof(Dialog_CommonKnowledge), "scrollPosition").GetValue(__instance);
            var selectedEntry = (CommonKnowledgeEntry)AccessTools.Field(typeof(Dialog_CommonKnowledge), "selectedEntry").GetValue(__instance);

            GUI.Box(rect, "");
            
            var filteredEntries = library.Entries.Where(e => 
                string.IsNullOrEmpty(searchFilter) || 
                e.tag.ToLower().Contains(searchFilter.ToLower()) ||
                e.content.ToLower().Contains(searchFilter.ToLower())
            ).ToList();
            
            var viewRect = new Rect(0f, 0f, rect.width - 16f, filteredEntries.Count * 70f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0f;

            foreach (var entry in filteredEntries)
            {
                Rect entryRect = new Rect(0f, y, viewRect.width, 65f);
                
                if (selectedEntry == entry)
                {
                    Widgets.DrawHighlight(entryRect);
                }
                
                // ⭐ 绘制我们的按钮
                DrawContentMatchingToggle(entry, entryRect);
                
                // ⭐ 绘制原版的其他 UI 元素
                Rect checkboxRect = new Rect(entryRect.x + 5f, entryRect.y + 10f, 24f, 24f);
                Widgets.Checkbox(checkboxRect.position, ref entry.isEnabled);
                
                Rect tagRect = new Rect(entryRect.x + 35f, entryRect.y + 5f, 100f, 20f);
                Widgets.Label(tagRect, $"[{entry.tag}]");
                
                Rect importanceRect = new Rect(entryRect.x + 140f, entryRect.y + 5f, 60f, 20f);
                Widgets.Label(importanceRect, entry.importance.ToString("F1"));
                
                Rect contentRect = new Rect(entryRect.x + 35f, entryRect.y + 25f, entryRect.width - 40f, 35f);
                Text.Font = GameFont.Tiny;
                string preview = entry.content.Length > 80 ? entry.content.Substring(0, 80) + "..." : entry.content;
                Widgets.Label(contentRect, preview);
                Text.Font = GameFont.Small;

                // ⭐ 将 ButtonInvisible 放在最后，避免覆盖我们的按钮
                if (Widgets.ButtonInvisible(entryRect))
                {
                    AccessTools.Field(typeof(Dialog_CommonKnowledge), "selectedEntry").SetValue(__instance, entry);
                    AccessTools.Field(typeof(Dialog_CommonKnowledge), "editMode").SetValue(__instance, false);
                }

                y += 70f;
            }

            Widgets.EndScrollView();
            
            if (!string.IsNullOrEmpty(searchFilter))
            {
                Rect searchResultRect = new Rect(rect.x, rect.yMax - 20f, rect.width, 20f);
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(searchResultRect, "RTExpPrev_SearchResult".Translate(filteredEntries.Count, library.Entries.Count));
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // 更新滚动位置
            AccessTools.Field(typeof(Dialog_CommonKnowledge), "scrollPosition").SetValue(__instance, scrollPosition);

            return false; // 阻止原方法执行
        }

        // 绘制切换按钮的静态方法
        public static void DrawContentMatchingToggle(CommonKnowledgeEntry entry, Rect entryRect)
        {
            // --- 按钮1：禁止/允许内容匹配 ---
            Rect matchButtonRect = new Rect(entryRect.xMax - 30f, entryRect.y + (entryRect.height / 2f) - 12f, 24f, 24f);
            bool isMatchingAllowed = KnowledgeExtractionData.IsContentMatchingAllowed(entry.id);
            Texture2D matchIcon = isMatchingAllowed ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
            string matchTooltip = isMatchingAllowed ? "允许内容匹配：此常识会与'常识内容关键词'进行匹配" : "禁止内容匹配：此常识不会与'常识内容关键词'进行匹配";

            if (Widgets.ButtonImage(matchButtonRect, matchIcon, Color.white, Color.cyan))
            {
                KnowledgeExtractionData.ToggleContentMatching(entry.id);
            }
            TooltipHandler.TipRegion(matchButtonRect, matchTooltip);

            // --- 按钮2：禁止/允许提取关键词 ---
            Rect extractButtonRect = new Rect(matchButtonRect.x - 30f, matchButtonRect.y, 24f, 24f);
            bool isExtractionAllowed = KnowledgeExtractionData.IsKeywordExtractionAllowed(entry.id);
            Texture2D extractIcon = isExtractionAllowed ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
            string extractTooltip = isExtractionAllowed ? "允许提取关键词：此常识的内容可以被提取为关键词" : "禁止提取关键词：此常识的内容不会被提取为关键词";

            if (Widgets.ButtonImage(extractButtonRect, extractIcon, Color.white, Color.yellow))
            {
                KnowledgeExtractionData.ToggleKeywordExtraction(entry.id);
            }
            TooltipHandler.TipRegion(extractButtonRect, extractTooltip);
        }
    }
    
    // 在删除和清空常识时，清理无效的数据
    [HarmonyPatch(typeof(Dialog_CommonKnowledge), "DeleteSelectedEntry")]
    public static class Patch_DeleteSelectedEntry
    {
        [HarmonyPostfix]
        public static void Postfix(Dialog_CommonKnowledge __instance)
        {
            var library = (CommonKnowledgeLibrary)AccessTools.Field(typeof(Dialog_CommonKnowledge), "library").GetValue(__instance);
            KnowledgeExtractionData.Cleanup(library.Entries.Select(e => e.id).ToList());
        }
    }

    [HarmonyPatch(typeof(Dialog_CommonKnowledge), "ClearAllEntries")]
    public static class Patch_ClearAllEntries
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            KnowledgeExtractionData.Cleanup(new List<string>());
        }
    }
}
