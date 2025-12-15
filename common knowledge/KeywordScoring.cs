using HarmonyLib;
using RimTalk.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace RimTalk_ExpandedPreview
{
    public static class KeywordScoring
    {
        public static List<string> ExtractAndScoreKeywords(string text, int maxKeywords, List<string> pawnKeywords = null)
        {
            var weightedKeywords = SuperKeywordEngine.ExtractKeywords(text, 100);

            if (pawnKeywords != null && pawnKeywords.Count > 0)
            {
                weightedKeywords.RemoveAll(kw => 
                {
                    foreach (var pk in pawnKeywords)
                    {
                        if (pk.IndexOf(kw.Word, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                    return false;
                });
            }
            
            var stopWordsField = AccessTools.Field(typeof(SuperKeywordEngine), "StopWords");
            var baseStopWords = (HashSet<string>)stopWordsField.GetValue(null);
            baseStopWords.UnionWith(new[] 
            { 
                "只", "个", "条", "名", "在", "了", "的", 
                "这", "那", "届时", "正在", "直接", "出现", "就", "以", "等", "和", "或", "被",
            }); 
            var startStopWords = new HashSet<string>(RimTalk_ExpandedPreviewMod.Settings.startStopWords);
            var endStopWords = new HashSet<string>(RimTalk_ExpandedPreviewMod.Settings.endStopWords);

            weightedKeywords.RemoveAll(kw =>
            {
                // 规则1: 3字及以下的短词包含停用词
                if (kw.Word.Length <= 3 && ContainsChinese(kw.Word))
                {
                    foreach (var stopWord in baseStopWords)
                    {
                        if (kw.Word.Contains(stopWord))
                            return true; // 移除
                    }
                }

                // 规则2: 关键词以停用词开头或结尾
                if (startStopWords.Any(startWord => kw.Word.StartsWith(startWord)))
                {
                    return true; // 移除
                }
                if (endStopWords.Any(endWord => kw.Word.EndsWith(endWord)))
                {
                    return true; // 移除
                }
                return false; // 保留
            });

            var keywordFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kw in weightedKeywords)
            {
                keywordFreq[kw.Word] = keywordFreq.ContainsKey(kw.Word) ? keywordFreq[kw.Word] + 1 : 1;
            }

            var scoredKeywords = new List<(string word, float score)>();
            foreach (var kw in weightedKeywords)
            {
                bool isChinese = ContainsChinese(kw.Word);
                float lengthWeight = CalculateLengthWeight(kw.Word, isChinese);
                float cohesionFactor = CalculateCohesionFactor(kw.Word, keywordFreq);
                float newScore = lengthWeight * cohesionFactor * (float)Math.Sqrt(kw.Weight + 0.1f);
                scoredKeywords.Add((kw.Word, newScore));
            }

            // // 1. 核心关键词 (取一半)
            // var coreKeywords = scoredKeywords
            //     .OrderByDescending(x => x.score)
            //     .Take(maxKeywords / 2)
            //     .ToList();

            // // 2. 模糊/多样性关键词 (取另一半)
            // var remainingPool = scoredKeywords.Except(coreKeywords).ToList();
            
            // var fuzzyKeywords = remainingPool
            //     .OrderBy(x => x.word, StringComparer.Ordinal) // 按字母顺序
            //     .Take(maxKeywords - coreKeywords.Count)
            //     .ToList();

            // // 3. 合并
            // var finalKeywords = new List<string>();
            // finalKeywords.AddRange(coreKeywords.Select(x => x.word));
            // finalKeywords.AddRange(fuzzyKeywords.Select(x => x.word));
            
            var finalKeywords = scoredKeywords
            .GroupBy(kv => kv.word, StringComparer.OrdinalIgnoreCase) 
            .Select(g => g.OrderByDescending(kv => kv.score).First()) 
            .OrderByDescending(kv => kv.score)
            .Select(kv => kv.word) 
            .Take(maxKeywords) 
            // .OrderByDescending(x => x.score)
            // .Take(maxKeywords)
            // .Select(x => x.word)
            .ToList();
            return finalKeywords;
        }

        private static float CalculateLengthWeight(string word, bool isChinese)
        {
            int length = word.Length;
            var settings = RimTalk_ExpandedPreviewMod.Settings;
            
            if (isChinese)
            {
                if (length == 2) return settings.chineseLength2Score;
                if (length == 3) return settings.chineseLength3Score;
                if (length == 4) return settings.chineseLength4Score;
                if (length == 5) return settings.chineseLength5Score;
                if (length == 6) return settings.chineseLength6Score;
                return 0.0f;
            }
            else
            {
                float weight = settings.englishBaseScore + (length - 1) * settings.englishIncrementScore;
                return Math.Min(weight, settings.englishMaxScore);
            }
        }

        private static float CalculateCohesionFactor(string word, Dictionary<string, int> keywordFreq)
        {
            if (word.Length <= 2)
                return 1.0f;

            var settings = RimTalk_ExpandedPreviewMod.Settings;
            float cohesionBonus = 0f;

            for (int len = 2; len <= 3 && len < word.Length; len++)
            {
                for (int i = 0; i <= word.Length - len; i++)
                {
                    string subword = word.Substring(i, len);
                    
                    if (keywordFreq.ContainsKey(subword))
                    {
                        cohesionBonus += settings.cohesionBonusPerMatch;
                    }
                }
            }

            cohesionBonus = Math.Min(cohesionBonus, settings.cohesionMaxBonus);
            
            return 1.0f + cohesionBonus;
        }

        private static bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            foreach (char c in text)
            {
                if (c >= 0x4e00 && c <= 0x9fa5)
                    return true;
            }
            
            return false;
        }
    }
}
