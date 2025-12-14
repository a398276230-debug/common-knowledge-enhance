// 文件功能：定义自定义关键词的数据结构。
// File: CustomKeywordData.cs
using Verse;
using System.Collections.Generic;

namespace RimTalk_ExpandedPreview
{
    public class CustomKeywordEntry : IExposable
    {
        public string keyword = "";
        public bool isEnabled = true;
        public bool matchWholeWord = false;
        public bool caseSensitive = false;
        public bool isNegative = false; // ⭐ 新增：标记为负面关键词

        public CustomKeywordEntry() { }

        // ⭐ 修改构造函数以包含 isNegative
        public CustomKeywordEntry(string word, bool enabled, bool wholeWord, bool caseSens, bool negative)
        {
            keyword = word;
            isEnabled = enabled;
            matchWholeWord = wholeWord;
            caseSensitive = caseSens;
            isNegative = negative; // ⭐ 赋值
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref keyword, "keyword", "");
            Scribe_Values.Look(ref isEnabled, "isEnabled", true);
            Scribe_Values.Look(ref matchWholeWord, "matchWholeWord", false);
            Scribe_Values.Look(ref caseSensitive, "caseSensitive", false);
            Scribe_Values.Look(ref isNegative, "isNegative", false); // ⭐ 序列化 isNegative
        }
    }

    // 这个静态类保持不变
    public static class CustomKeywordSettings
    {
        public static List<CustomKeywordEntry> CustomKeywords = new List<CustomKeywordEntry>();

        public static void Load(List<CustomKeywordEntry> loadedKeywords)
        {
            if (loadedKeywords != null)
            {
                CustomKeywords = loadedKeywords;
            }
            else
            {
                CustomKeywords = new List<CustomKeywordEntry>();
            }
        }

        public static void Set(List<CustomKeywordEntry> keywords)
        {
            CustomKeywords = keywords;
        }
    }
}
