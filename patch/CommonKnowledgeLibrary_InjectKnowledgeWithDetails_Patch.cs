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
    // 重构为 Postfix 补丁
    [HarmonyPatch]
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
            // 如果禁用了循环功能，则直接返回原方法的结果，不做任何事
            if (!RimTalk_ExpandedPreviewMod.Settings.enableKnowledgeCycle || RimTalk_ExpandedPreviewMod.Settings.knowledgeExtractionCycles <= 0)
            {
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

            // 初始关键词来自原方法执行后的 keywordInfo
            List<string> combinedContextKeywords = new List<string>(keywordInfo.ContextKeywords);
            if (keywordInfo.PawnInfo != null)
            {
                // 将Pawn关键词也合并进来
                var pawnKeywords = GetAllPawnKeywords(keywordInfo.PawnInfo);
                combinedContextKeywords.AddRange(pawnKeywords);
            }
            // 提取目标Pawn的关键词
            if (targetPawn != null && targetPawn != currentPawn)
            {
                 var extractPawnKeywordsMethod = AccessTools.Method(typeof(CommonKnowledgeLibrary), "ExtractPawnKeywordsWithDetails");
                 var targetPawnInfo = (PawnKeywordInfo)extractPawnKeywordsMethod.Invoke(__instance, new object[] { new List<string>(), targetPawn });
                 combinedContextKeywords.AddRange(GetAllPawnKeywords(targetPawnInfo));
            }
            combinedContextKeywords = combinedContextKeywords.Distinct().ToList();


            // 循环逻辑
            float knowledgeScoreThreshold = RimTalkMemoryPatchMod.Settings?.knowledgeScoreThreshold ?? 0.1f;
            int cycles = RimTalk_ExpandedPreviewMod.Settings.knowledgeExtractionCycles;
            HashSet<string> processedEntryIds = new HashSet<string>();
            List<KnowledgeScoreDetail> finalScoreDetails = new List<KnowledgeScoreDetail>();
            var pawnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (currentPawn != null && !string.IsNullOrEmpty(currentPawn.Name?.ToStringShort)) pawnNames.Add(currentPawn.Name.ToStringShort);
            if (targetPawn != null && targetPawn != currentPawn && !string.IsNullOrEmpty(targetPawn.Name?.ToStringShort)) pawnNames.Add(targetPawn.Name.ToStringShort);

            HashSet<string> extractedContentKnowledgeIds = new HashSet<string>();

            // 循环从1开始，因为第0次（原版方法）已经执行过了
            for (int i = 0; i < cycles; i++)
            {
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
                    .Take(maxEntries - processedEntryIds.Count)
                    .ToList();

                if (!relevantKnowledgeThisCycle.Any()) break;

                List<string> newKeywordsThisCycle = new List<string>();
                foreach (var detail in relevantKnowledgeThisCycle)
                {
                    if (processedEntryIds.Add(detail.Entry.id))
                    {
                        finalScoreDetails.Add(detail);

                        if (!string.IsNullOrEmpty(detail.Entry.content))
                        {
                            var keywords = SuperKeywordEngine.ExtractKeywords(detail.Entry.content, 100)
                                .OrderByDescending(p => p.Word.Length)
                                .ThenBy(p => p.Word, StringComparer.Ordinal)
                                .Take(RimTalk_ExpandedPreviewMod.Settings.knowledgeContentKeywordLimit)
                                .Select(wk => wk.Word)
                                .ToList();
                            
                            newKeywordsThisCycle.AddRange(keywords);
                            extractedContentKnowledgeIds.Add(detail.Entry.id);
                        }
                    }
                }

                if (!newKeywordsThisCycle.Any()) break;

                var distinctNewKeywords = newKeywordsThisCycle.Distinct().Where(k => !combinedContextKeywords.Contains(k)).ToList();
                if (!distinctNewKeywords.Any()) break; // 如果没有真正的新关键词，则结束

                SharedPatchData.ExtractedKnowledgeContentKeywords.AddRange(distinctNewKeywords);
                combinedContextKeywords.AddRange(distinctNewKeywords);
            }

            // 将未处理的常识的最终分数也加入 allScores，以便 UI 显示
            var unprocessedScores = libraryEntries
                .Where(entry => entry.isEnabled && !processedEntryIds.Contains(entry.id))
                .Select(entry => entry.CalculateRelevanceScoreWithDetails(combinedContextKeywords, pawnNames));
            
            finalScoreDetails.AddRange(unprocessedScores);

            // 为被提取了内容的常识添加额外分数
            float bonus = RimTalk_ExpandedPreviewMod.Settings.extractedContentKnowledgeBonus;
            if (bonus > 0f)
            {
                foreach (var detail in finalScoreDetails)
                {
                    if (extractedContentKnowledgeIds.Contains(detail.Entry.id))
                    {
                        detail.TotalScore += bonus;
                        detail.FailReason += $" (ExtractedBonus: +{bonus:F2})";
                    }
                }
            }

            // 覆盖 out 参数 allScores
            allScores = finalScoreDetails.OrderByDescending(se => se.TotalScore).ToList();

            // 覆盖 out 参数 scores
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
