// 文件功能：提取关键词的Harmony补丁。
// File: ExtractKeywordsPatch.cs
using HarmonyLib;
using RimTalk.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk_ExpandedPreview
{
    // =========================================================================
    // Patch 1: 添加【正面】自定义关键词 (已有，保持不变)
    // =========================================================================
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "ExtractContextKeywords", new System.Type[] { typeof(string) })]
    public static class CommonKnowledgeLibrary_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string text, ref List<string> __result)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // ⭐ 修改点：从新的管理器获取所有合并后的关键词
            var allCustomKeywords = MECP_KeywordsManager.GetActiveKeywords();
            if (allCustomKeywords == null || !allCustomKeywords.Any())
            {
                return;
            }
            // ⭐ 只处理【正面】关键词
            var positiveKeywords = allCustomKeywords.Where(e => e.isEnabled && !e.isNegative).ToList();
            if (!positiveKeywords.Any())
            {
                return;
            }

            // ... 后续逻辑保持不变 ...
            if (__result == null)
                __result = new List<string>();
            var keywordsToInject = new List<string>();
            foreach (var customEntry in positiveKeywords)
            {
                if (string.IsNullOrEmpty(customEntry.keyword))
                    continue;
                bool isPresentInText = CheckKeywordPresence(text, customEntry);
                if (!isPresentInText)
                {
                    continue;
                }
                bool alreadyExistsInResult = __result.Any(k => string.Equals(k, customEntry.keyword, System.StringComparison.OrdinalIgnoreCase));
                if (alreadyExistsInResult)
                {
                    continue;
                }
                keywordsToInject.Add(customEntry.keyword);
            }
            if (!keywordsToInject.Any())
            {
                return;
            }
            __result.InsertRange(0, keywordsToInject);
            const int maxFinalKeywords = 20;
            __result = __result
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .Take(maxFinalKeywords)
                .ToList();
        }


        // 辅助方法保持不变
        private static bool CheckKeywordPresence(string text, CustomKeywordEntry entry)
        {
            System.StringComparison comparison = entry.caseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase;
            if (entry.matchWholeWord)
            {
                if (ContainsCjk(entry.keyword))
                {
                    return text.IndexOf(entry.keyword, comparison) >= 0;
                }
                else
                {
                    RegexOptions regexOptions = entry.caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    Regex wholeWordRegex = new Regex($"\\b{Regex.Escape(entry.keyword)}\\b", regexOptions);
                    return wholeWordRegex.IsMatch(text);
                }
            }
            else
            {
                return text.IndexOf(entry.keyword, comparison) >= 0;
            }
        }

        private static bool ContainsCjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0xF900 && c <= 0xFAFF))
                {
                    return true;
                }
            }
            return false;
        }
    }


    // =========================================================================
    // ⭐ Patch 2: 移除【负面】自定义关键词 (新增)
    // =========================================================================
    // 目标：SuperKeywordEngine.ExtractKeywords
    // 职责：在SuperKeywordEngine生成100个初始关键词后，如果上下文中存在负面关键词，
    //       则从这100个关键词中移除所有匹配的负面关键词。
    [HarmonyPatch(typeof(SuperKeywordEngine), "ExtractKeywords")]
    public static class SuperKeywordEngine_NegativePatch
    {
        // 同样需要这两个辅助方法，可以直接复制过来
        private static bool CheckKeywordPresence(string text, CustomKeywordEntry entry)
        {
            System.StringComparison comparison = entry.caseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase;
            if (entry.matchWholeWord)
            {
                if (ContainsCjk(entry.keyword))
                {
                    return text.IndexOf(entry.keyword, comparison) >= 0;
                }
                else
                {
                    RegexOptions regexOptions = entry.caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    Regex wholeWordRegex = new Regex($"\\b{Regex.Escape(entry.keyword)}\\b", regexOptions);
                    return wholeWordRegex.IsMatch(text);
                }
            }
            else
            {
                return text.IndexOf(entry.keyword, comparison) >= 0;
            }
        }

        private static bool ContainsCjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0xF900 && c <= 0xFAFF))
                {
                    return true;
                }
            }
            return false;
        }

        [HarmonyPostfix]
        public static void Postfix(string text, ref List<WeightedKeyword> __result)
        {
            if (string.IsNullOrEmpty(text) || __result == null || !__result.Any())
            {
                return;
            }

            // ⭐ 修改点：从新的管理器获取所有合并后的关键词
            var allCustomKeywords = MECP_KeywordsManager.GetActiveKeywords();
            if (allCustomKeywords == null || !allCustomKeywords.Any())
            {
                return;
            }

            // 1. 找出所有启用的【负面】关键词
            var negativeKeywords = allCustomKeywords.Where(e => e.isEnabled && e.isNegative).ToList();
            if (!negativeKeywords.Any())
            {
                return;
            }
            // ... 后续逻辑保持不变 ...
            var activeNegativeTriggers = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var negEntry in negativeKeywords)
            {
                if (string.IsNullOrEmpty(negEntry.keyword)) continue;
                if (CheckKeywordPresence(text, negEntry))
                {
                    activeNegativeTriggers.Add(negEntry.keyword);
                }
            }

            if (!activeNegativeTriggers.Any())
            {
                return;
            }
            int removedCount = __result.RemoveAll(weightedKeyword =>
            {
                // 检查当前关键词是否与任何一个激活的负面触发词存在N元包含关系
                foreach (var trigger in activeNegativeTriggers)
                {
                    // 如果关键词包含触发词，或触发词包含关键词，则视为匹配
                    if (weightedKeyword.Word.IndexOf(trigger, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        trigger.IndexOf(weightedKeyword.Word, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            });
            if (removedCount > 0 && Prefs.DevMode)
            {
                Log.Message($"[RimTalk_ExpandedPreview] Negative keyword filter active. Removed {removedCount} keywords from initial list because context contained triggers: {string.Join(", ", activeNegativeTriggers)}");
            }
        }
    }
}
