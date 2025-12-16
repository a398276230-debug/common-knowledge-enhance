using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimTalk.Memory;
using RimTalk.Memory.UI;
using RimTalk.CommonKnowledgeEnhance;

namespace RimTalk.CommonKnowledgeEnhance.Patches
{
    /// <summary>
    /// 为Dialog_CommonKnowledge的每个常识条目添加两个按钮：
    /// 1. 允许被提取内容（×/√）
    /// 2. 允许被匹配（×/√）
    /// </summary>
    [HarmonyPatch(typeof(Dialog_CommonKnowledge), "DrawEntryRow")]
    public static class DialogCommonKnowledgePatch
    {
        private const float BUTTON_SIZE = 20f;
        private const float BUTTON_SPACING = 5f;

        /// <summary>
        /// 后缀补丁：在原始方法执行后添加复选框
        /// </summary>
        static void Postfix(Rect rect, CommonKnowledgeEntry entry)
        {
            if (entry == null)
                return;

            // 在条目最右侧添加两个复选框
            float rightX = rect.xMax - BUTTON_SIZE * 2 - BUTTON_SPACING - 10f;
            float centerY = rect.y + (rect.height - BUTTON_SIZE) / 2f;

            // 复选框1：允许被提取内容
            bool canBeExtracted = ExtendedKnowledgeEntry.CanBeExtracted(entry);
            Rect extractCheckboxRect = new Rect(rightX, centerY, BUTTON_SIZE, BUTTON_SIZE);
            
            // 鼠标悬停提示
            if (Mouse.IsOver(extractCheckboxRect))
            {
                string tooltip = canBeExtracted 
                    ? "RimTalkEP_CanBeExtractedEnabled".Translate() 
                    : "RimTalkEP_CanBeExtractedDisabled".Translate();
                TooltipHandler.TipRegion(extractCheckboxRect, tooltip);
            }
            
            // 使用Widgets.Checkbox绘制复选框
            bool newExtractValue = canBeExtracted;
            Widgets.Checkbox(extractCheckboxRect.position, ref newExtractValue, BUTTON_SIZE);
            if (newExtractValue != canBeExtracted)
            {
                ExtendedKnowledgeEntry.SetCanBeExtracted(entry, newExtractValue);
            }

            // 复选框2：允许被匹配
            rightX += BUTTON_SIZE + BUTTON_SPACING;
            bool canBeMatched = ExtendedKnowledgeEntry.CanBeMatched(entry);
            Rect matchCheckboxRect = new Rect(rightX, centerY, BUTTON_SIZE, BUTTON_SIZE);
            
            // 鼠标悬停提示
            if (Mouse.IsOver(matchCheckboxRect))
            {
                string tooltip = canBeMatched 
                    ? "RimTalkEP_CanBeMatchedEnabled".Translate() 
                    : "RimTalkEP_CanBeMatchedDisabled".Translate();
                TooltipHandler.TipRegion(matchCheckboxRect, tooltip);
            }
            
            // 使用Widgets.Checkbox绘制复选框
            bool newMatchValue = canBeMatched;
            Widgets.Checkbox(matchCheckboxRect.position, ref newMatchValue, BUTTON_SIZE);
            if (newMatchValue != canBeMatched)
            {
                ExtendedKnowledgeEntry.SetCanBeMatched(entry, newMatchValue);
            }
        }
    }

    /// <summary>
    /// 在详细面板中显示扩展属性
    /// </summary>
    [HarmonyPatch(typeof(Dialog_CommonKnowledge), "DrawDetailPanel")]
    public static class DialogDetailPanelPatch
    {
        /// <summary>
        /// 后缀补丁：在详细面板底部添加扩展属性显示
        /// </summary>
        static void Postfix(Rect rect, CommonKnowledgeEntry entry)
        {
            if (entry == null)
                return;

            // 在详细面板底部添加扩展属性信息
            float y = rect.yMax - 120f;

            // 分隔线
            Widgets.DrawLineHorizontal(rect.x, y - 10f, rect.width);
            y += 5f;

            // 标题
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), "RimTalkEP_ExtendedProperties".Translate());
            GUI.color = Color.white;
            y += 25f;

            // 允许被提取
            bool canBeExtracted = ExtendedKnowledgeEntry.CanBeExtracted(entry);
            DrawPropertyRow(new Rect(rect.x, y, rect.width, 25f), "RimTalkEP_CanBeExtracted".Translate(), canBeExtracted);
            y += 25f;

            // 允许被匹配
            bool canBeMatched = ExtendedKnowledgeEntry.CanBeMatched(entry);
            DrawPropertyRow(new Rect(rect.x, y, rect.width, 25f), "RimTalkEP_CanBeMatched".Translate(), canBeMatched);
        }

        private static void DrawPropertyRow(Rect rect, string label, bool value)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(rect.x, rect.y, 120f, rect.height), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            string valueText = value ? "RimTalkEP_Yes".Translate() : "RimTalkEP_No".Translate();
            Color valueColor = value ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            
            GUI.color = valueColor;
            Widgets.Label(new Rect(rect.x + 120f, rect.y, rect.width - 120f, rect.height), valueText);
            GUI.color = Color.white;
        }
    }


    /// <summary>
    /// 在多选面板底部添加批量操作按钮
    /// </summary>
    [HarmonyPatch(typeof(Dialog_CommonKnowledge), "DrawMultiSelectionPanel")]
    public static class DialogMultiSelectionPanelPatch
    {
        private const float BUTTON_HEIGHT = 32f;

        /// <summary>
        /// 后缀补丁：在多选面板底部添加批量操作按钮
        /// </summary>
        static void Postfix(Rect rect, Dialog_CommonKnowledge __instance)
        {
            // 通过反射获取selectedEntries
            var selectedEntriesField = AccessTools.Field(typeof(Dialog_CommonKnowledge), "selectedEntries");
            var selectedEntries = selectedEntriesField?.GetValue(__instance) as HashSet<CommonKnowledgeEntry>;

            if (selectedEntries == null || selectedEntries.Count == 0)
                return;

            // 在面板底部添加批量操作按钮
            float y = rect.yMax - BUTTON_HEIGHT * 4 - 25f; // 4个按钮 + 间距

            // 分隔线
            Widgets.DrawLineHorizontal(rect.x, y - 10f, rect.width);
            y += 5f;

            // 全部开启提取
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalkEP_EnableAllExtract".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeExtracted(entry, true);
                }
            }
            y += BUTTON_HEIGHT + 5f;

            // 全部关闭提取
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalkEP_DisableAllExtract".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeExtracted(entry, false);
                }
            }
            y += BUTTON_HEIGHT + 5f;

            // 全部开启匹配
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalkEP_EnableAllMatch".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeMatched(entry, true);
                }
            }
            y += BUTTON_HEIGHT + 5f;

            // 全部关闭匹配
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, BUTTON_HEIGHT), "RimTalkEP_DisableAllMatch".Translate()))
            {
                foreach (var entry in selectedEntries)
                {
                    ExtendedKnowledgeEntry.SetCanBeMatched(entry, false);
                }
            }
        }
    }
}
