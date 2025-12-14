using System.Collections.Generic;
using System.Linq;

namespace RimTalk_ExpandedPreview
{
    // 文件功能：管理MECP模组的关键词。
    public static class MECP_KeywordsManager
    {
        /// <summary>
        /// 返回合并后的关键词列表：以全局关键词为基础，若存档关键词中存在同名关键词（忽略大小写），则以存档关键词覆盖全局关键词。
        /// 不会对启用/负面等做过滤，调用方负责筛选。
        /// </summary>
        public static List<CustomKeywordEntry> GetActiveKeywords()
        {
            var result = new List<CustomKeywordEntry>();

            var global = CustomKeywordSettings.CustomKeywords ?? new List<CustomKeywordEntry>();
            // 复制全局列表（深拷贝不是严格必需，但避免引用同一实例）
            foreach (var g in global)
            {
                result.Add(new CustomKeywordEntry(g.keyword, g.isEnabled, g.matchWholeWord, g.caseSensitive, g.isNegative));
            }

            var comp = SaveGameKeywordComponent.ForCurrent();
            if (comp == null || comp.saveKeywords == null || comp.saveKeywords.Count == 0)
            {
                return result;
            }

            // 将存档关键词合并：若 keyword 名称与全局重复（忽略大小写），则覆盖；否则追加
            foreach (var s in comp.saveKeywords)
            {
                if (string.IsNullOrEmpty(s.keyword))
                {
                    continue;
                }

                var idx = result.FindIndex(x => string.Equals(x.keyword, s.keyword, System.StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    // 覆盖全局条目
                    result[idx] = new CustomKeywordEntry(s.keyword, s.isEnabled, s.matchWholeWord, s.caseSensitive, s.isNegative);
                }
                else
                {
                    result.Add(new CustomKeywordEntry(s.keyword, s.isEnabled, s.matchWholeWord, s.caseSensitive, s.isNegative));
                }
            }

            return result;
        }
    }
}
