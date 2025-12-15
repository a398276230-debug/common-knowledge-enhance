// 常识触发常识
using HarmonyLib;
using RimTalk.Memory;
using RimTalk.MemoryPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;

namespace RimTalk_ExpandedPreview
{
    // ⭐ 添加 using 语句
    using RimTalk_ExpandedPreview;
    // 重构为 Postfix 补丁
    // ⭐ 设置高优先级，确保在 UI 捕获补丁之前执行
    [HarmonyPatch]
    [HarmonyPriority(Priority.High)]
    public static class CommonKnowledgeLibrary_InjectKnowledgeWithDetails_Patch
    {
        // 使用 TargetMethod 动态定位复杂签名的方法
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CommonKnowledgeLibrary), "InjectKnowledgeWithDetails",
                new Type[] { typeof(string), typeof(int), typeof(List<KnowledgeScore>).MakeByRefType(), typeof(List<KnowledgeScoreDetail>).MakeByRefType(), typeof(KeywordExtractionInfo).MakeByRefType(), typeof(Pawn), typeof(Pawn) },
                null);
        }

        [HarmonyPostfix]
        public static void Postfix(
            CommonKnowledgeLibrary __instance,
            string context,
            int maxEntries,
            ref List<KnowledgeScore> scores,
            ref List<KnowledgeScoreDetail> allScores,
            ref KeywordExtractionInfo keywordInfo,
            Pawn currentPawn,
            Pawn targetPawn,
            ref string __result)
        {
            // ⭐ 调试日志
            if (Prefs.DevMode)
            {
                Log.Message($"[MECP Patch] Postfix called. enableKnowledgeCycle={RimTalk_ExpandedPreviewMod.Settings.enableKnowledgeCycle}, cycles={RimTalk_ExpandedPreviewMod.Settings.knowledgeExtractionCycles}, bonus={RimTalk_ExpandedPreviewMod.Settings.extractedContentKnowledgeBonus}");
            }

            // 如果禁用了循环功能，则直接返回原方法的结果，不做任何事
            if (!RimTalk_ExpandedPreviewMod.Settings.enableKnowledgeCycle || RimTalk_ExpandedPreviewMod.Settings.knowledgeExtractionCycles <= 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Message("[MECP Patch] Cycle disabled, returning early");
                }
                return;
            }

            // 清理共享数据
            SharedPatchData.ExtractedKnowledgeContentKeywords.Clear();

            // 从实例中获取私有字段 'entries'
            var libraryEntries = (List<CommonKnowledgeEntry>)AccessTools.Field(typeof(CommonKnowledgeLibrary), "entries").GetValue(__instance);
            if (!libraryEntries.Any())
            {
                return; // 没有常识，直接返回
            }

            // ⭐ 利用原版方法已经计算好的结果作为起点
            List<string> combinedContextKeywords = new List<string>(keywordInfo.ContextKeywords);
            if (keywordInfo.PawnInfo != null)
            {
                var pawnKeywords = GetAllPawnKeywords(keywordInfo.PawnInfo);
                combinedContextKeywords.AddRange(pawnKeywords);
            }
            if (targetPawn != null && targetPawn != currentPawn)
            {
                 var extractPawnKeywordsMethod = AccessTools.Method(typeof(CommonKnowledgeLibrary), "ExtractPawnKeywordsWithDetails");
                 var targetPawnInfo = (PawnKeywordInfo)extractPawnKeywordsMethod.Invoke(__instance, new object[] { new List<string>(), targetPawn });
                 combinedContextKeywords.AddRange(GetAllPawnKeywords(targetPawnInfo));
            }
            combinedContextKeywords = combinedContextKeywords.Distinct().ToList();

            float knowledgeScoreThreshold = RimTalkMemoryPatchMod.Settings?.knowledgeScoreThreshold ?? 0.1f;
            int cycles = RimTalk_ExpandedPreviewMod.Settings.knowledgeExtractionCycles;
            var pawnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (currentPawn != null && !string.IsNullOrEmpty(currentPawn.Name?.ToStringShort)) pawnNames.Add(currentPawn.Name.ToStringShort);
            if (targetPawn != null && targetPawn != currentPawn && !string.IsNullOrEmpty(targetPawn.Name?.ToStringShort)) pawnNames.Add(targetPawn.Name.ToStringShort);

            // ⭐ 从原版已经选中的常识开始（第0轮）
            HashSet<string> processedEntryIds = new HashSet<string>(scores.Select(s => s.Entry.id));
            var extractedEntriesOriginalScores = new Dictionary<string, KnowledgeScoreDetail>();
            
            // ⭐ 从原版已选中的常识中提取关键词，并保存原始分数
            // ⭐ 应用 maxExtractableKnowledge 限制
            foreach (var score in scores.Take(RimTalk_ExpandedPreviewMod.Settings.maxExtractableKnowledge))
            {
                // ⭐ 检查是否允许提取关键词
                if (KnowledgeExtractionData.IsKeywordExtractionAllowed(score.Entry.id) && !string.IsNullOrEmpty(score.Entry.content))
                {
                    var keywords = RimTalk_ExpandedPreviewMod.Settings.useNewKeywordLogic
                        ? KeywordScoring.ExtractAndScoreKeywords(score.Entry.content, RimTalk_ExpandedPreviewMod.Settings.knowledgeContentKeywordLimit)
                        : SuperKeywordEngine.ExtractKeywords(score.Entry.content, 100)
                            .OrderByDescending(p => p.Word.Length)
                            .ThenBy(p => p.Word, StringComparer.Ordinal)
                            .Take(RimTalk_ExpandedPreviewMod.Settings.knowledgeContentKeywordLimit)
                            .Select(wk => wk.Word)
                            .ToList();
                    
                    if (keywords.Any())
                    {
                        var distinctNewKeywords = keywords.Where(k => !combinedContextKeywords.Contains(k)).ToList();
                        if (distinctNewKeywords.Any())
                        {
                            SharedPatchData.ExtractedKnowledgeContentKeywords.AddRange(distinctNewKeywords);
                            combinedContextKeywords.AddRange(distinctNewKeywords);
                            
                            // ⭐ 保存原始分数（从 allScores 中查找）
                            var originalDetail = allScores.FirstOrDefault(d => d.Entry.id == score.Entry.id);
                            if (originalDetail != null)
                            {
                                extractedEntriesOriginalScores[score.Entry.id] = originalDetail;
                                
                                if (Prefs.DevMode)
                                {
                                    Log.Message($"[MECP Patch] Extracted {keywords.Count} keywords from '{score.Entry.id}' (score: {originalDetail.TotalScore:F3}): {string.Join(", ", keywords.Take(3))}");
                                }
                            }
                        }
                    }
                }
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[MECP Patch] After processing original scores: {extractedEntriesOriginalScores.Count} entries marked for bonus");
            }

            // ⭐ 继续进行额外的循环（如果设置了）
            // cycles-1 是因为第0轮（原版）已经提取过一次了
            if (Prefs.DevMode)
            {
                Log.Message($"[MECP Patch] Starting additional cycles: {cycles} cycles configured (will do {cycles-1} additional extract rounds)");
            }
            
            for (int i = 0; i < cycles - 1; i++)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[MECP Patch] === Cycle {i+1}/{cycles-1} starting ===");
                }
                
                // 使用当前的关键词集合重新评分
                var currentScores = libraryEntries
                    .Where(entry => entry.isEnabled && !processedEntryIds.Contains(entry.id))
                    .Select(entry => entry.CalculateRelevanceScoreWithDetails(combinedContextKeywords, pawnNames))
                    .ToList();

                var relevantKnowledgeThisCycle = currentScores
                    .Where(detail => detail.TotalScore >= knowledgeScoreThreshold)
                    .OrderByDescending(se => se.TotalScore)
                    .ThenByDescending(se => se.KeywordMatchCount)
                    .ThenBy(se => se.Entry.id, StringComparer.Ordinal)
                    .Take(RimTalk_ExpandedPreviewMod.Settings.maxExtractableKnowledge - processedEntryIds.Count)
                    .ToList();

                if (!relevantKnowledgeThisCycle.Any())
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[MECP Patch] Cycle {i+1} ended: no relevant knowledge found");
                    }
                    break;
                }

                bool hasNewKeywords = false;
                foreach (var detail in relevantKnowledgeThisCycle)
                {
                    if (processedEntryIds.Add(detail.Entry.id))
                    {
                        // ⭐ 检查是否允许提取关键词
                        if (KnowledgeExtractionData.IsKeywordExtractionAllowed(detail.Entry.id) && !string.IsNullOrEmpty(detail.Entry.content))
                        {
                            var keywords = (RimTalk_ExpandedPreviewMod.Settings.useNewKeywordLogic
                                ? KeywordScoring.ExtractAndScoreKeywords(detail.Entry.content, RimTalk_ExpandedPreviewMod.Settings.knowledgeContentKeywordLimit)
                                : SuperKeywordEngine.ExtractKeywords(detail.Entry.content, 100)
                                    .OrderByDescending(p => p.Word.Length)
                                    .ThenBy(p => p.Word, StringComparer.Ordinal)
                                    .Take(RimTalk_ExpandedPreviewMod.Settings.knowledgeContentKeywordLimit)
                                    .Select(wk => wk.Word)
                                    .ToList())
                                .Where(k => !combinedContextKeywords.Contains(k))
                                .ToList();
                            
                            if (keywords.Any())
                            {
                                SharedPatchData.ExtractedKnowledgeContentKeywords.AddRange(keywords);
                                combinedContextKeywords.AddRange(keywords);
                                
                                // ⭐ 保存这个常识的原始分数（循环中发现的）
                                extractedEntriesOriginalScores[detail.Entry.id] = detail;
                                hasNewKeywords = true;
                                
                                if (Prefs.DevMode)
                                {
                                    Log.Message($"[MECP Patch Cycle {i+1}] Extracted {keywords.Count} keywords from '{detail.Entry.id}' (score: {detail.TotalScore:F3})");
                                }
                            }
                        }
                    }
                }

                if (!hasNewKeywords)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[MECP Patch] Cycle {i+1} ended: no new keywords extracted");
                    }
                    break;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[MECP Patch] Cycle {i+1} completed successfully");
                }
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[MECP Patch] All cycles completed. Total extracted entries: {extractedEntriesOriginalScores.Count}");
            }

            // ⭐ 重新评分所有常识（使用扩展后的关键词）
            var originalKeywordsForNonMatching = new List<string>(keywordInfo.ContextKeywords); // ⭐ 提取到 lambda 外部
            if (keywordInfo.PawnInfo != null)
            {
                originalKeywordsForNonMatching.AddRange(GetAllPawnKeywords(keywordInfo.PawnInfo));
            }
            var finalScoreDetails = libraryEntries
                .Where(entry => entry.isEnabled)
                .Select(entry => {
                    // ⭐ 如果是已提取的常识，直接返回原始分数
                    if (extractedEntriesOriginalScores.TryGetValue(entry.id, out var originalDetail))
                    {
                        return originalDetail;
                    }
                    
                    // ⭐ 检查是否允许内容匹配
                    if (KnowledgeExtractionData.IsContentMatchingAllowed(entry.id))
                    {
                        return entry.CalculateRelevanceScoreWithDetails(combinedContextKeywords, pawnNames);
                    }
                    
                    // ⭐ 如果不允许，则使用原始上下文关键词重新评分
                    return entry.CalculateRelevanceScoreWithDetails(originalKeywordsForNonMatching, pawnNames);
                })
                .ToList();

            // ⭐ 为被提取了内容的常识添加额外分数
            float bonus = RimTalk_ExpandedPreviewMod.Settings.extractedContentKnowledgeBonus;
            int bonusAppliedCount = 0;
            if (bonus > 0f && extractedEntriesOriginalScores.Any())
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[MECP Patch] Applying bonus: {bonus}, to {extractedEntriesOriginalScores.Count} entries");
                }
                
                foreach (var detail in finalScoreDetails)
                {
                    if (extractedEntriesOriginalScores.ContainsKey(detail.Entry.id))
                    {
                        float oldScore = detail.TotalScore;
                        detail.TotalScore += bonus;
                        detail.FailReason += $" (ExtractedBonus: +{bonus:F2})";
                        bonusAppliedCount++;
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[MECP Patch] Applied bonus to '{detail.Entry.id}': {oldScore:F3} -> {detail.TotalScore:F3}");
                        }
                    }
                }
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[MECP Patch] Bonus applied to {bonusAppliedCount} entries");
            }

            // ⭐ 覆盖 out 参数
            allScores = finalScoreDetails.OrderByDescending(se => se.TotalScore).ToList();
            scores = allScores
                .Where(se => se.TotalScore >= knowledgeScoreThreshold)
                .Take(maxEntries)
                .Select(detail => new KnowledgeScore { Entry = detail.Entry, Score = detail.TotalScore })
                .ToList();

            SharedPatchData.ExtractedKnowledgeContentKeywords = SharedPatchData.ExtractedKnowledgeContentKeywords.Distinct().ToList();

            // 覆盖 __result
            if (scores.Any())
            {
                StringBuilder sb = new StringBuilder();
                int index = 1;
                foreach (var scored in scores)
                {
                    sb.AppendLine($"{index}. [{scored.Entry.tag}] {scored.Entry.content}");
                }
                __result = sb.ToString();
            }
            else
            {
                __result = null;
            }
        }
        

        // 辅助方法，用于从PawnKeywordInfo中提取所有关键词
        private static IEnumerable<string> GetAllPawnKeywords(PawnKeywordInfo info)
        {
            if (info == null) yield break;
            foreach (var kw in info.NameKeywords) yield return kw;
            foreach (var kw in info.AgeKeywords) yield return kw;
            foreach (var kw in info.GenderKeywords) yield return kw;
            foreach (var kw in info.RaceKeywords) yield return kw;
            foreach (var kw in info.TraitKeywords) yield return kw;
            foreach (var kw in info.SkillKeywords) yield return kw;
            foreach (var kw in info.SkillLevelKeywords) yield return kw;
            foreach (var kw in info.HealthKeywords) yield return kw;
            foreach (var kw in info.RelationshipKeywords) yield return kw;
            foreach (var kw in info.BackstoryKeywords) yield return kw;
            foreach (var kw in info.ChildhoodKeywords) yield return kw;
        }
    }
}
