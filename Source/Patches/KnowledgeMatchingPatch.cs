using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimTalk.Memory;
using RimTalk.CommonKnowledgeEnhance;

namespace RimTalk.CommonKnowledgeEnhance.Patches
{
    /// <summary>
    /// 拦截并替换前置mod的常识匹配逻辑
    /// 实现新的标签匹配方式（类似世界书）+ 常识链
    /// </summary>
    [HarmonyPatch]
    public static class KnowledgeMatchingPatch
    {
        /// <summary>
        /// 手动指定要patch的方法（处理重载方法）
        /// 返回所有需要patch的重载版本
        /// </summary>
        static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();

            // 方法1：最完整的重载版本（带所有out参数）
            var method1 = typeof(CommonKnowledgeLibrary).GetMethod(
                "InjectKnowledgeWithDetails",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] {
                    typeof(string),                                         // context
                    typeof(int),                                            // maxEntries
                    typeof(List<KnowledgeScore>).MakeByRefType(),          // out scores
                    typeof(List<KnowledgeScoreDetail>).MakeByRefType(),    // out allScores
                    typeof(KeywordExtractionInfo).MakeByRefType(),         // out keywordInfo
                    typeof(Verse.Pawn),                                     // currentPawn
                    typeof(Verse.Pawn)                                      // targetPawn
                },
                null
            );

            if (method1 != null)
            {
                methods.Add(method1);
                Log.Message("[RimTalk-CommonKnowledgeEnhance] Found target method: InjectKnowledgeWithDetails (full version)");
            }

            // 方法2：SmartInjectionManager使用的版本（只有out scores）
            var method2 = typeof(CommonKnowledgeLibrary).GetMethod(
                "InjectKnowledgeWithDetails",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] {
                    typeof(string),                                // context
                    typeof(int),                                   // maxEntries
                    typeof(List<KnowledgeScore>).MakeByRefType(), // out scores
                    typeof(Verse.Pawn),                            // currentPawn
                    typeof(Verse.Pawn)                             // targetPawn
                },
                null
            );

            if (method2 != null)
            {
                methods.Add(method2);
                Log.Message("[RimTalk-CommonKnowledgeEnhance] Found target method: InjectKnowledgeWithDetails (simple version)");
            }

            if (methods.Count == 0)
            {
                Log.Error("[RimTalk-CommonKnowledgeEnhance] Failed to find any target methods!");
            }

            return methods;
        }

        /// <summary>
        /// 前缀补丁：完全替换原始方法（完整版本）
        /// </summary>
        static bool Prefix(
            CommonKnowledgeLibrary __instance,
            string context,
            int maxEntries,
            ref List<KnowledgeScore> scores,
            Verse.Pawn currentPawn,
            Verse.Pawn targetPawn,
            ref string __result,
            MethodBase __originalMethod)
        {
            // 如果未启用新匹配逻辑，使用原始方法
            if (!RimTalkCommonKnowledgeEnhance.Settings.useNewTagMatching)
            {
                return true; // 继续执行原始方法
            }

            // 初始化scores
            scores = new List<KnowledgeScore>();
            
            // 检查方法签名，确定是哪个重载版本
            var parameters = __originalMethod.GetParameters();
            bool hasAllScores = parameters.Any(p => p.Name == "allScores");
            bool hasKeywordInfo = parameters.Any(p => p.Name == "keywordInfo");

            List<KnowledgeScoreDetail> allScores;
            KeywordExtractionInfo keywordInfo;

            // 使用新的匹配逻辑
            __result = NewMatchingLogic(__instance, context, maxEntries, out scores, out allScores, out keywordInfo, currentPawn, targetPawn);
            
            return false; // 跳过原始方法
        }

        /// <summary>
        /// 新的匹配逻辑：只匹配标签，支持常识链
        /// </summary>
        private static string NewMatchingLogic(
            CommonKnowledgeLibrary library,
            string context,
            int maxEntries,
            out List<KnowledgeScore> scores,
            out List<KnowledgeScoreDetail> allScores,
            out KeywordExtractionInfo keywordInfo,
            Verse.Pawn currentPawn,
            Verse.Pawn targetPawn)
        {
            scores = new List<KnowledgeScore>();
            allScores = new List<KnowledgeScoreDetail>();
            keywordInfo = new KeywordExtractionInfo();

            if (library.Entries.Count == 0)
                return string.Empty;

            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            
            // 构建完整的匹配文本（上下文 + Pawn信息）
            StringBuilder matchTextBuilder = new StringBuilder();
            matchTextBuilder.Append(context);
            
            if (currentPawn != null)
            {
                matchTextBuilder.Append(" ");
                matchTextBuilder.Append(BuildPawnInfoText(currentPawn));
            }
            
            if (targetPawn != null && targetPawn != currentPawn)
            {
                matchTextBuilder.Append(" ");
                matchTextBuilder.Append(BuildPawnInfoText(targetPawn));
            }
            
            string originalMatchText = matchTextBuilder.ToString();
            string currentMatchText = originalMatchText;
            
            keywordInfo.ContextKeywords = new List<string> { context };
            keywordInfo.TotalKeywords = 1;
            keywordInfo.PawnKeywordsCount = 0;

            var allMatchedEntries = new HashSet<CommonKnowledgeEntry>();
            
            // 多轮匹配（常识链）
            int maxRounds = settings.enableKnowledgeChaining ? settings.maxChainingRounds : 1;
            
            for (int round = 0; round < maxRounds; round++)
            {
                if (string.IsNullOrEmpty(currentMatchText))
                    break;

                bool isChaining = round > 0;
                string matchText = (round == 0) ? originalMatchText : currentMatchText;
                var roundMatches = MatchKnowledgeByTags(library, matchText, currentPawn, allMatchedEntries, isChaining);
                
                if (roundMatches.Count == 0)
                    break;

                foreach (var match in roundMatches)
                {
                    allMatchedEntries.Add(match);
                }

                if (!settings.enableKnowledgeChaining || round >= maxRounds - 1)
                    break;

                currentMatchText = BuildMatchTextFromKnowledge(roundMatches);
            }
            
            // 按重要性排序并限制数量
            var sortedEntries = allMatchedEntries
                .OrderByDescending(e => e.importance)
                .ThenBy(e => e.id, StringComparer.Ordinal)
                .Take(maxEntries)
                .ToList();

            // 生成评分信息
            foreach (var entry in sortedEntries)
            {
                float finalScore = 0.5f + entry.importance;  // 匹配到常识就评分0.5f+重要性
                
                scores.Add(new KnowledgeScore
                {
                    Entry = entry,
                    Score = finalScore
                });

                allScores.Add(new KnowledgeScoreDetail
                {
                    Entry = entry,
                    IsEnabled = entry.isEnabled,
                    TotalScore = finalScore,
                    ImportanceScore = entry.importance,
                    MatchedTags = entry.GetTags(),
                    FailReason = "Tag matched"
                });
            }

            if (sortedEntries.Count == 0)
                return null;

            var sb = new StringBuilder();
            int index = 1;
            foreach (var entry in sortedEntries)
            {
                sb.AppendLine($"{index}. [{entry.tag}] {entry.content}");
                index++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 通过标签匹配常识（新版：支持 Any/All 匹配模式）
        /// </summary>
        /// <param name="isChaining">是否是常识链匹配（第2轮及以后）</param>
        private static List<CommonKnowledgeEntry> MatchKnowledgeByTags(
            CommonKnowledgeLibrary library,
            string matchText,
            Verse.Pawn currentPawn,
            HashSet<CommonKnowledgeEntry> alreadyMatched,
            bool isChaining = false)
        {
            var matches = new List<CommonKnowledgeEntry>();

            if (string.IsNullOrEmpty(matchText))
                return matches;

            foreach (var entry in library.Entries)
            {
                // 跳过已匹配的
                if (alreadyMatched.Contains(entry))
                    continue;

                // 跳过未启用的
                if (!entry.isEnabled)
                    continue;

                // ⭐ 只在常识链中检查是否允许被匹配
                if (isChaining && !ExtendedKnowledgeEntry.CanBeMatched(entry))
                    continue;

                // 检查Pawn限制
                if (entry.targetPawnId != -1 && (currentPawn == null || entry.targetPawnId != currentPawn.thingIDNumber))
                    continue;

                // ⭐ 使用新的 IsMatched 方法（支持 Any/All 模式）
                if (IsMatched(matchText, entry))
                {
                    matches.Add(entry);
                }
            }

            return matches;
        }

        /// <summary>
        /// 检查文本是否匹配常识条目（支持 Any/All 匹配模式）
        /// </summary>
        private static bool IsMatched(string text, CommonKnowledgeEntry entry)
        {
            var tags = entry.GetTags();
            if (tags == null || tags.Count == 0) return false;

            KeywordMatchMode matchMode = ExtendedKnowledgeEntry.GetMatchMode(entry);

            switch (matchMode)
            {
                case KeywordMatchMode.Any:
                    // Any 模式：只要匹配任一标签即可
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    return false;

                case KeywordMatchMode.All:
                    // All 模式：必须匹配所有标签
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 从匹配的常识构建新的匹配文本（用于常识链）
        /// </summary>
        private static string BuildMatchTextFromKnowledge(List<CommonKnowledgeEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var entry in entries)
            {
                // 检查是否允许被提取
                if (!ExtendedKnowledgeEntry.CanBeExtracted(entry))
                    continue;

                // 直接使用常识的内容
                if (!string.IsNullOrEmpty(entry.content))
                {
                    if (sb.Length > 0)
                        sb.Append(" ");
                    sb.Append(entry.content);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// ⭐ 通过向量匹配常识（补充标签未匹配的语义相关常识）
        /// </summary>
        private static List<CommonKnowledgeEntry> MatchKnowledgeByVector(
            CommonKnowledgeLibrary library,
            string context,
            Verse.Pawn currentPawn,
            HashSet<CommonKnowledgeEntry> alreadyMatched,
            int maxResults,
            float similarityThreshold)
        {
            var matches = new List<CommonKnowledgeEntry>();

            if (string.IsNullOrEmpty(context))
                return matches;

            try
            {
                // 调用向量服务查找相似常识
                var vectorResults = CommonKnowledgeEnhance.Vector.VectorService.Instance.FindBestLoreIds(context, maxResults * 2);  // 多取一些，因为可能有已匹配的
                
                foreach (var (id, similarity) in vectorResults)
                {
                    // 检查相似度阈值
                    if (similarity < similarityThreshold)
                        continue;
                    
                    // 查找对应的常识条目
                    var entry = library.Entries.FirstOrDefault(e => e.id == id);
                    
                    if (entry == null)
                        continue;
                    
                    // 跳过已匹配的
                    if (alreadyMatched.Contains(entry))
                        continue;
                    
                    // 跳过未启用的
                    if (!entry.isEnabled)
                        continue;
                    
                    // 检查Pawn限制
                    if (entry.targetPawnId != -1 && (currentPawn == null || entry.targetPawnId != currentPawn.thingIDNumber))
                        continue;
                    
                    // 添加到结果
                    matches.Add(entry);
                    
                    Log.Message($"[RimTalk-CommonKnowledgeEnhance] Vector match: {entry.tag} (similarity: {similarity:F4})");
                    
                    // 达到最大数量就停止
                    if (matches.Count >= maxResults)
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-CommonKnowledgeEnhance] Error in MatchKnowledgeByVector: {ex}");
            }

            return matches;
        }
        
        /// <summary>
        /// 构建Pawn信息文本
        /// </summary>
        private static string BuildPawnInfoText(Verse.Pawn pawn)
        {
            if (pawn == null)
                return string.Empty;

            var sb = new StringBuilder();

            try
            {
                // 1. 名字
                if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
                {
                    sb.Append(pawn.Name.ToStringShort);
                    sb.Append(" ");
                }

                // 2. 性别
                sb.Append(pawn.gender.GetLabel());
                sb.Append(" ");

                // 3. 种族
                if (pawn.def != null)
                {
                    sb.Append(pawn.def.label);
                    sb.Append(" ");
                }

                // 4. 特质（最多5个）
                if (pawn.story?.traits != null)
                {
                    int traitCount = 0;
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait?.def?.label != null && traitCount < 5)
                        {
                            sb.Append(trait.def.label);
                            sb.Append(" ");
                            traitCount++;
                        }
                    }
                }

                // 5. 技能（只提取高等级技能，10级以上）
                if (pawn.skills != null)
                {
                    foreach (var skillRecord in pawn.skills.skills)
                    {
                        if (skillRecord.TotallyDisabled || skillRecord.Level < 10)
                            continue;

                        if (skillRecord.def?.label != null)
                        {
                            sb.Append(skillRecord.def.label);
                            sb.Append(" ");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-CommonKnowledgeEnhance] Error building pawn info text: {ex.Message}");
            }

            return sb.ToString().Trim();
        }
    }
}
