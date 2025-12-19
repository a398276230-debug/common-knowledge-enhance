using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.Memory;
using RimTalk.Memory.Debug;
using RimTalk.CommonKnowledgeEnhance;
using RimTalk.CommonKnowledgeEnhance.Vector;

namespace RimTalk.CommonKnowledgeEnhance.Patches
{
    /// <summary>
    /// 调试预览器补丁 - 添加向量匹配测试功能
    /// </summary>
    public static class DialogInjectionPreviewPatch
    {
        /// <summary>
        /// Patch: DrawContextInput
        /// 在上下文输入区域添加"测试向量匹配"按钮（在"读取上次输入"按钮下方）
        /// </summary>
        [HarmonyPatch(typeof(Dialog_InjectionPreview), "DrawContextInput")]
        public static class Patch_DrawContextInput
        {
            static void Postfix(Dialog_InjectionPreview __instance, Rect rect)
            {
                // 通过反射获取 contextInput 字段
                var contextInputField = typeof(Dialog_InjectionPreview).GetField("contextInput", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (contextInputField == null)
                    return;

                string contextInput = contextInputField.GetValue(__instance) as string;
                
                // 在"读取上次输入"按钮下方添加向量测试按钮
                // 参考原版位置：rect.x + rect.width - 150f, rect.y（读取上次输入）
                // 向量测试按钮放在下方：rect.y + 35f
                Rect vectorButtonRect = new Rect(rect.x + rect.width - 150f, rect.y + 35f, 140f, 30f);
                
                var settings = RimTalkCommonKnowledgeEnhance.Settings;
                bool vectorEnabled = settings.enableVectorEnhancement;
                
                GUI.enabled = vectorEnabled && !string.IsNullOrEmpty(contextInput);
                
                if (Widgets.ButtonText(vectorButtonRect, "🧠 测试向量匹配"))
                {
                    TestVectorMatching(__instance, contextInput);
                }
                
                GUI.enabled = true;
                
                if (!vectorEnabled)
                {
                    TooltipHandler.TipRegion(vectorButtonRect, "向量增强功能未启用\n请在Mod设置中开启");
                }
                else if (string.IsNullOrEmpty(contextInput))
                {
                    TooltipHandler.TipRegion(vectorButtonRect, "请先输入上下文内容");
                }
                else
                {
                    TooltipHandler.TipRegion(vectorButtonRect, 
                        "将上下文内容发送到向量库进行匹配测试\n" +
                        "点击后会弹出窗口显示匹配结果");
                }
            }
        }

        /// <summary>
        /// 测试向量匹配（弹出新窗口显示结果）
        /// </summary>
        private static void TestVectorMatching(Dialog_InjectionPreview instance, string contextInput)
        {
            if (string.IsNullOrEmpty(contextInput))
            {
                Messages.Message("请先输入上下文内容", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            if (!settings.enableVectorEnhancement)
            {
                Messages.Message("向量增强功能未启用，请在设置中开启", MessageTypeDefOf.RejectInput, false);
                return;
            }

            try
            {
                Log.Message($"[RimTalk-VectorTest] Starting vector matching test...");
                
                // ⭐ 使用 ContextCleaner 清理上下文
                string cleanedContext = ContextCleaner.CleanForVectorMatching(contextInput);
                
                if (string.IsNullOrEmpty(cleanedContext))
                {
                    Log.Warning($"[RimTalk-VectorTest] Context cleaned to empty, using original");
                    cleanedContext = contextInput;
                }
                else
                {
                    Log.Message($"[RimTalk-VectorTest] Cleaned context: {cleanedContext.Substring(0, Math.Min(100, cleanedContext.Length))}...");
                }

                // ⚠️ 在主线程同步等待异步结果（预览界面可以接受卡顿）
                // ⭐ 降低阈值，让更多候选进入，后续用综合评分过滤
                float lowThreshold = Math.Max(0.5f, settings.vectorSimilarityThreshold - 0.2f);
                
                var vectorResults = VectorService.Instance.FindBestLoreIdsAsync(
                    cleanedContext,
                    settings.maxVectorResults * 3,  // ⬅️ 多取一些候选
                    lowThreshold  // ⬅️ 使用较低的阈值
                ).Result;  // ⬅️ 同步等待

                if (vectorResults == null || vectorResults.Count == 0)
                {
                    Messages.Message($"未找到相似度 >= {settings.vectorSimilarityThreshold:F2} 的常识", 
                        MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Log.Message($"[RimTalk-VectorTest] Found {vectorResults.Count} vector matches");
                    
                    // ⭐ 弹出新窗口显示结果（参考原版实现）
                    ShowVectorResults(instance, vectorResults, cleanedContext);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-VectorTest] Vector matching failed: {ex}");
                Messages.Message($"向量匹配失败: {ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>
        /// 弹出新窗口显示向量匹配结果
        /// </summary>
        private static void ShowVectorResults(Dialog_InjectionPreview instance, List<(string id, float similarity)> results, string cleanedContext)
        {
            var library = MemoryManager.GetCommonKnowledge();
            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            
            // ⭐ 去重逻辑：获取已被关键词匹配的条目ID
            var keywordMatchedIds = new HashSet<string>();
            try
            {
                // 通过反射获取 selectedPawn 和 targetPawn
                var selectedPawnField = typeof(Dialog_InjectionPreview).GetField("selectedPawn", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var targetPawnField = typeof(Dialog_InjectionPreview).GetField("targetPawn", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                Pawn selectedPawn = selectedPawnField?.GetValue(instance) as Pawn;
                Pawn targetPawn = targetPawnField?.GetValue(instance) as Pawn;
                
                library.InjectKnowledgeWithDetails(
                    cleanedContext,
                    settings.maxVectorResults,
                    out var keywordScores,
                    selectedPawn,
                    targetPawn
                );
                
                if (keywordScores != null)
                {
                    foreach (var score in keywordScores)
                    {
                        keywordMatchedIds.Add(score.Entry.id);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-VectorTest] Failed to get keyword matches for deduplication: {ex.Message}");
            }
            
            // ⭐ 综合评分过滤：结合相似度和重要性
            var scoredResults = new List<(CommonKnowledgeEntry Entry, float Similarity, float Score, bool IsDuplicate)>();
            
            foreach (var (id, similarity) in results)
            {
                var entry = library.Entries.FirstOrDefault(e => e.id == id);
                if (entry != null)
                {
                    // 计算综合评分
                    float score = similarity + (entry.importance * 0.2f);
                    bool isDuplicate = keywordMatchedIds.Contains(id);
                    
                    // ⭐ 用综合评分判断是否通过阈值（重要性现在真正参与过滤）
                    if (score >= settings.vectorSimilarityThreshold)
                    {
                        scoredResults.Add((entry, similarity, score, isDuplicate));
                    }
                }
            }
            
            // 按综合得分排序，取前 maxVectorResults 个
            var finalResults = scoredResults
                .OrderByDescending(x => x.Score)
                .Take(settings.maxVectorResults)
                .ToList();
            
            var sb = new StringBuilder();
            sb.AppendLine("【向量匹配测试结果】");
            sb.AppendLine($"候选: {results.Count} → 通过综合阈值: {scoredResults.Count} → 最终: {finalResults.Count}");
            sb.AppendLine($"阈值: {settings.vectorSimilarityThreshold:F2} (综合评分 = 相似度 + 重要性×0.2)");
            sb.AppendLine();
            
            if (finalResults.Count == 0)
            {
                sb.AppendLine("⚠️ 没有条目通过综合评分阈值");
                sb.AppendLine($"提示: 降低阈值或增加常识重要性");
            }
            else
            {
                foreach (var item in finalResults)
                {
                    string duplicateTag = item.IsDuplicate ? " [已被关键词匹配]" : "";
                    sb.AppendLine($"[相似:{item.Similarity:F4}|综合:{item.Score:F4}] [{item.Entry.tag}] {item.Entry.content}{duplicateTag}");
                }
            }
            
            // ⭐ 弹出新窗口显示结果
            Find.WindowStack.Add(new Dialog_MessageBox(sb.ToString()));
        }
    }
}
