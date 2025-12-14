// File: PawnKeywordExtractionPatch.cs
// 文件功能：重写Pawn信息关键词的提取逻辑，以更好地处理英文短语并集成负面关键词过滤。

using HarmonyLib;
using RimTalk.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;
using RimWorld;
using System;

namespace RimTalk_ExpandedPreview
{
    [HarmonyPatch(typeof(CommonKnowledgeLibrary))]
    [HarmonyPatch("ExtractPawnKeywordsWithDetails")]
    public static class PawnKeywordExtraction_Patch
    {
        // Regex to extract whole words, including English, Chinese, and numbers.
        // This prevents cutting English words in half.
        private static readonly Regex WordExtractionRegex = new Regex(@"\b[\p{L}\p{N}_]+\b", RegexOptions.Compiled);

        /// <summary>
        /// 前缀补丁，完全重写原版的Pawn关键词提取方法。
        /// 1. 优化背景故事（Backstory）的关键词提取，避免英文单词被切碎。
        /// 2. 在提取结束时，应用负面关键词过滤，确保Pawn信息生成的关键词也能被正确过滤。
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(CommonKnowledgeLibrary __instance, ref PawnKeywordInfo __result, List<string> keywords, Pawn pawn)
        {
            var info = new PawnKeywordInfo
            {
                PawnName = pawn.LabelShort
            };

            if (pawn == null || keywords == null)
            {
                __result = info;
                return false; // 跳过原方法
            }

            try
            {
                // 1. 名字
                if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
                {
                    var name = pawn.Name.ToStringShort;
                    AddAndRecord(name, keywords, info.NameKeywords);
                }

                // 2. 年龄段
                if (pawn.RaceProps?.Humanlike ?? false)
                {
                    float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                    if (ageYears < 3f) AddAndRecord("婴儿", keywords, info.AgeKeywords);
                    else if (ageYears < 13f) AddAndRecord("儿童", keywords, info.AgeKeywords);
                    else if (ageYears < 18f) AddAndRecord("青少年", keywords, info.AgeKeywords);
                    else AddAndRecord("成人", keywords, info.AgeKeywords);
                }

                // 3. 性别
                if (pawn.gender != Gender.None)
                {
                    AddAndRecord(pawn.gender.GetLabel(), keywords, info.GenderKeywords);
                }


                // 4. 种族
                if (pawn.def != null)
                {
                    AddAndRecord(pawn.def.label, keywords, info.RaceKeywords);
                    if (pawn.genes?.Xenotype != null)
                    {
                        string xenotypeName = pawn.genes.Xenotype.label ?? pawn.genes.Xenotype.defName;
                        if (!string.IsNullOrEmpty(xenotypeName))
                        {
                            AddAndRecord(xenotypeName, keywords, info.RaceKeywords);
                            AddAndRecord($"{pawn.def.label}-{xenotypeName}", keywords, info.RaceKeywords);
                        }
                    }
                }

                // 5. 特质
                if (pawn.story?.traits != null)
                {
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait?.def?.label != null)
                        {
                            AddAndRecord(trait.def.label, keywords, info.TraitKeywords);
                        }
                    }
                }

                // 6. 技能
                if (pawn.skills != null)
                {
                    foreach (var skillRecord in pawn.skills.skills)
                    {
                        if (skillRecord.TotallyDisabled) continue;
                        if (skillRecord.def?.label == null) continue;

                        AddAndRecord(skillRecord.def.label, keywords, info.SkillKeywords);
                        int level = skillRecord.Level;
                        AddAndRecord($"{skillRecord.def.label}{level}级", keywords, info.SkillKeywords);
                        AddAndRecord($"{level}级", keywords, info.SkillLevelKeywords);

                        if (level >= 15) AddAndRecord("精通", keywords, info.SkillLevelKeywords);
                        else if (level >= 10) AddAndRecord("熟练", keywords, info.SkillLevelKeywords);
                    }
                }

                // 7. 健康状况
                if (pawn.health?.hediffSet != null)
                {
                    if (pawn.health.hediffSet.GetInjuredParts().Any()) AddAndRecord("受伤", keywords, info.HealthKeywords);
                    if (pawn.health.hediffSet.HasNaturallyHealingInjury()) AddAndRecord("恢复中", keywords, info.HealthKeywords);
                    if (pawn.health.summaryHealth.SummaryHealthPercent > 0.9f) AddAndRecord("健康", keywords, info.HealthKeywords);
                }

                // 8. 关系 (⭐ 更新为新版逻辑)
                if (pawn.relations != null)
                {
                    var allRelatedPawns = pawn.relations.RelatedPawns
                        .Where(rp => rp != null && rp.thingIDNumber >= 0)
                        .ToList();

                    var importantRelationDefs = new HashSet<PawnRelationDef>
                    {
                        PawnRelationDefOf.Spouse, PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance,
                        PawnRelationDefOf.Parent, PawnRelationDefOf.Child, PawnRelationDefOf.Sibling
                    };

                    var importantPawns = allRelatedPawns
                        .Where(rp => pawn.relations.DirectRelations.Any(dr => dr.otherPawn == rp && importantRelationDefs.Contains(dr.def)))
                        .OrderBy(rp => rp.thingIDNumber)
                        .ToList();

                    try
                    {
                        var bondedAnimals = new List<Pawn>();
                        if (Find.Maps != null)
                        {
                            foreach (var map in Find.Maps)
                            {
                                if (map?.mapPawns?.AllPawns == null) continue;
                                foreach (var animal in map.mapPawns.AllPawns.Where(p => p.RaceProps?.Animal ?? false))
                                {
                                    if (animal.relations != null && animal.relations.DirectRelationExists(PawnRelationDefOf.Bond, pawn))
                                    {
                                        bondedAnimals.Add(animal);
                                    }
                                }
                            }
                        }
                        importantPawns.AddRange(bondedAnimals.OrderBy(ba => ba.thingIDNumber));
                    }
                    catch (Exception) { /* 兼容性：忽略Bond关系相关的错误 */ }

                    var selectedPawns = importantPawns.Distinct().Take(5).ToList();
                    if (selectedPawns.Count < 5)
                    {
                        var remainingPawns = allRelatedPawns.Except(selectedPawns).ToList();
                        var random = new System.Random(pawn.thingIDNumber);
                        selectedPawns.AddRange(remainingPawns.OrderBy(x => random.Next()).Take(5 - selectedPawns.Count));
                    }

                    foreach (var relatedPawn in selectedPawns)
                    {
                        if (!string.IsNullOrEmpty(relatedPawn.Name?.ToStringShort))
                        {
                            AddAndRecord(relatedPawn.Name.ToStringShort, keywords, info.RelationshipKeywords);
                        }

                        var directRelations = pawn.relations.DirectRelations
                            .Where(r => r.otherPawn == relatedPawn)
                            .OrderBy(r => GetRelationPriority(r.def))
                            .Take(2);

                        foreach (var relation in directRelations)
                        {
                            if (!string.IsNullOrEmpty(relation.def?.label))
                            {
                                AddAndRecord(relation.def.label, keywords, info.RelationshipKeywords);
                            }
                        }
                    }
                }

                // 9. 成年背景 (⭐ 新的提取逻辑)
                if (pawn.story?.Adulthood != null)
                {
                    string backstoryTitle = pawn.story.Adulthood.TitleShortFor(pawn.gender);
                    ExtractWordsFromTitle(backstoryTitle, keywords, info.BackstoryKeywords);
                }

                // 10. 童年背景 (⭐ 新的提取逻辑)
                if (pawn.story?.Childhood != null)
                {
                    string childhoodTitle = pawn.story.Childhood.TitleShortFor(pawn.gender);
                    ExtractWordsFromTitle(childhoodTitle, keywords, info.ChildhoodKeywords);
                }

                // ⭐ 在最后应用负面关键词过滤
                ApplyNegativeKeywordFilter(keywords);

                info.TotalCount = info.NameKeywords.Count + info.AgeKeywords.Count + info.GenderKeywords.Count +
                                 info.RaceKeywords.Count + info.TraitKeywords.Count + info.SkillKeywords.Count +
                                 info.SkillLevelKeywords.Count + info.HealthKeywords.Count +
                                 info.RelationshipKeywords.Count + info.BackstoryKeywords.Count + info.ChildhoodKeywords.Count;

            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk_ExpandedPreview] Error in PawnKeywordExtraction_Patch: {ex.Message}\n{ex.StackTrace}");
            }

            __result = info;
            return false; // 阻止原方法执行
        }

        /// <summary>
        /// ⭐ 新增：获取关系优先级（从新版代码迁移）
        /// </summary>
        private static int GetRelationPriority(PawnRelationDef relationDef)
        {
            if (relationDef == null) return 999;
            if (relationDef == PawnRelationDefOf.Spouse) return 0;
            if (relationDef == PawnRelationDefOf.Lover) return 1;
            if (relationDef == PawnRelationDefOf.Fiance) return 2;
            if (relationDef == PawnRelationDefOf.Parent) return 10;
            if (relationDef == PawnRelationDefOf.Child) return 11;
            if (relationDef == PawnRelationDefOf.Sibling) return 12;
            if (relationDef == PawnRelationDefOf.Bond) return 20;
            return 100;
        }

        /// <summary>
        /// 从标题中提取关键词。
        /// 1. 添加完整标题。
        /// 2. 使用正则表达式提取所有独立的单词（英文、数字等）。
        /// </summary>
        private static void ExtractWordsFromTitle(string title, List<string> allKeywords, List<string> categoryKeywords)
        {
            if (string.IsNullOrEmpty(title)) return;

            // 添加完整标题作为一个关键词
            AddAndRecord(title, allKeywords, categoryKeywords);

            // 使用正则表达式提取单词，避免切分
            MatchCollection matches = WordExtractionRegex.Matches(title);
            foreach (Match match in matches)
            {
                if (match.Success && !string.IsNullOrEmpty(match.Value))
                {
                    // 只有当提取出的单词不是标题本身时才添加，避免重复
                    if (!string.Equals(match.Value, title, StringComparison.OrdinalIgnoreCase))
                    {
                        AddAndRecord(match.Value, allKeywords, categoryKeywords);
                    }
                }
            }
        }

        /// <summary>
        /// 添加关键词并记录到分类列表（避免重复）
        /// </summary>
        private static void AddAndRecord(string keyword, List<string> allKeywords, List<string> categoryKeywords)
        {
            if (string.IsNullOrEmpty(keyword)) return;

            // 统一转换为小写以进行比较和存储，但可以根据需要调整
            string processedKeyword = keyword.Trim();
            if (string.IsNullOrEmpty(processedKeyword)) return;

            if (!allKeywords.Contains(processedKeyword, StringComparer.OrdinalIgnoreCase))
            {
                allKeywords.Add(processedKeyword);
            }
            if (!categoryKeywords.Contains(processedKeyword, StringComparer.OrdinalIgnoreCase))
            {
                categoryKeywords.Add(processedKeyword);
            }
        }

        /// <summary>
        /// 应用负面关键词过滤器。
        /// 此逻辑从 SuperKeywordEngine_NegativePatch 借鉴而来，确保Pawn关键词也被过滤。
        /// </summary>
        private static void ApplyNegativeKeywordFilter(List<string> keywords)
        {
            if (keywords == null || !keywords.Any()) return;

            var allCustomKeywords = MECP_KeywordsManager.GetActiveKeywords();
            if (allCustomKeywords == null || !allCustomKeywords.Any()) return;

            var negativeKeywords = allCustomKeywords.Where(e => e.isEnabled && e.isNegative).ToList();
            if (!negativeKeywords.Any()) return;

            // 这里我们没有完整的上下文文本，所以我们假设如果一个负面关键词的“触发词”
            // 出现在任何地方，我们都应该移除它。
            // 为了简化，我们直接移除所有被标记为负面的关键词。
            var negativeKeywordSet = new HashSet<string>(negativeKeywords.Select(k => k.keyword), StringComparer.OrdinalIgnoreCase);

            int removedCount = keywords.RemoveAll(k => negativeKeywordSet.Contains(k));

            if (removedCount > 0 && Prefs.DevMode)
            {
                Log.Message($"[RimTalk_ExpandedPreview] Pawn keyword filter active. Removed {removedCount} negative keywords from pawn info.");
            }
        }
    }
}
