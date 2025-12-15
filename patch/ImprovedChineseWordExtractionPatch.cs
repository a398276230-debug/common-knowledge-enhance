using HarmonyLib;
using RimTalk.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace RimTalk_ExpandedPreview
{
    /// <summary>
    /// 改进关键词提取：停用词过滤 + 新评分系统
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "ExtractContextKeywords")]
    public static class ImprovedChineseWordExtractionPatch
    {
    [HarmonyPostfix]
        public static void Postfix(string text, ref List<string> __result)
        {
            if (!RimTalk_ExpandedPreviewMod.Settings.useNewKeywordLogic)
                return;

            if (Prefs.DevMode)
            {
                Log.Warning($"[ImprovedExtraction] Patch EXECUTED! Input text: {text.Substring(0, Math.Min(50, text.Length))}..., Initial keywords: {__result.Count}");
            }
            
            if (string.IsNullOrEmpty(text))
                return;

            __result = KeywordScoring.ExtractAndScoreKeywords(text, 20);

            if (Prefs.DevMode)
            {
                Log.Message($"[ImprovedExtraction] Final {__result.Count} keywords. Top 5: {string.Join(", ", __result.Take(5))}");
            }
        }
    }
}
