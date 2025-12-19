using System;
using Verse;
using RimTalk.Memory;

namespace RimTalk.CommonKnowledgeEnhance
{
    /// <summary>
    /// 关键词匹配模式
    /// </summary>
    public enum KeywordMatchMode
    {
        Any,    // 单词匹配：只要出现其中一个词就算
        All     // 组合匹配：必须同时出现所有标签
    }

    /// <summary>
    /// 扩展的常识条目
    /// 添加两个新属性：
    /// - canBeExtracted: 是否允许被提取内容（用于常识链）
    /// - canBeMatched: 是否允许被匹配
    /// </summary>
    public static class ExtendedKnowledgeEntry
    {
        // 使用字典存储扩展属性（避免修改原始类）
        private static System.Collections.Generic.Dictionary<string, ExtendedProperties> extendedProps 
            = new System.Collections.Generic.Dictionary<string, ExtendedProperties>();

        public class ExtendedProperties
        {
            public bool canBeExtracted = false;  // 默认禁止被提取
            public bool canBeMatched = false;    // 默认禁止被匹配
            public KeywordMatchMode matchMode = KeywordMatchMode.Any;  // 匹配模式（默认Any）
        }

        /// <summary>
        /// 获取扩展属性
        /// </summary>
        public static ExtendedProperties GetExtendedProperties(CommonKnowledgeEntry entry)
        {
            if (entry == null)
                return new ExtendedProperties();

            if (!extendedProps.ContainsKey(entry.id))
            {
                extendedProps[entry.id] = new ExtendedProperties();
            }

            return extendedProps[entry.id];
        }

        /// <summary>
        /// 设置是否允许被提取
        /// </summary>
        public static void SetCanBeExtracted(CommonKnowledgeEntry entry, bool value)
        {
            if (entry == null) return;
            GetExtendedProperties(entry).canBeExtracted = value;
        }

        /// <summary>
        /// 设置是否允许被匹配
        /// </summary>
        public static void SetCanBeMatched(CommonKnowledgeEntry entry, bool value)
        {
            if (entry == null) return;
            GetExtendedProperties(entry).canBeMatched = value;
        }

        /// <summary>
        /// 获取是否允许被提取
        /// </summary>
        public static bool CanBeExtracted(CommonKnowledgeEntry entry)
        {
            if (entry == null) return false;
            return GetExtendedProperties(entry).canBeExtracted;
        }

        /// <summary>
        /// 获取是否允许被匹配
        /// </summary>
        public static bool CanBeMatched(CommonKnowledgeEntry entry)
        {
            if (entry == null) return false;
            return GetExtendedProperties(entry).canBeMatched;
        }

        /// <summary>
        /// 设置匹配模式
        /// </summary>
        public static void SetMatchMode(CommonKnowledgeEntry entry, KeywordMatchMode mode)
        {
            if (entry == null) return;
            GetExtendedProperties(entry).matchMode = mode;
        }

        /// <summary>
        /// 获取匹配模式
        /// </summary>
        public static KeywordMatchMode GetMatchMode(CommonKnowledgeEntry entry)
        {
            if (entry == null) return KeywordMatchMode.Any;
            return GetExtendedProperties(entry).matchMode;
        }

        /// <summary>
        /// 清理已删除条目的扩展属性
        /// </summary>
        public static void CleanupDeletedEntries(CommonKnowledgeLibrary library)
        {
            if (library == null) return;

            var validIds = new System.Collections.Generic.HashSet<string>();
            foreach (var entry in library.Entries)
            {
                if (entry != null)
                    validIds.Add(entry.id);
            }

            var keysToRemove = new System.Collections.Generic.List<string>();
            foreach (var key in extendedProps.Keys)
            {
                if (!validIds.Contains(key))
                    keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
            {
                extendedProps.Remove(key);
            }
        }

        /// <summary>
        /// 保存扩展属性到存档
        /// </summary>
        public static void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var keys = new System.Collections.Generic.List<string>(extendedProps.Keys);
                var canBeExtractedList = new System.Collections.Generic.List<bool>();
                var canBeMatchedList = new System.Collections.Generic.List<bool>();
                var matchModeList = new System.Collections.Generic.List<KeywordMatchMode>();

                foreach (var key in keys)
                {
                    canBeExtractedList.Add(extendedProps[key].canBeExtracted);
                    canBeMatchedList.Add(extendedProps[key].canBeMatched);
                    matchModeList.Add(extendedProps[key].matchMode);
                }

                Scribe_Collections.Look(ref keys, "extendedKnowledgeKeys", LookMode.Value);
                Scribe_Collections.Look(ref canBeExtractedList, "canBeExtractedList", LookMode.Value);
                Scribe_Collections.Look(ref canBeMatchedList, "canBeMatchedList", LookMode.Value);
                Scribe_Collections.Look(ref matchModeList, "matchModeList", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var keys = new System.Collections.Generic.List<string>();
                var canBeExtractedList = new System.Collections.Generic.List<bool>();
                var canBeMatchedList = new System.Collections.Generic.List<bool>();
                var matchModeList = new System.Collections.Generic.List<KeywordMatchMode>();

                Scribe_Collections.Look(ref keys, "extendedKnowledgeKeys", LookMode.Value);
                Scribe_Collections.Look(ref canBeExtractedList, "canBeExtractedList", LookMode.Value);
                Scribe_Collections.Look(ref canBeMatchedList, "canBeMatchedList", LookMode.Value);
                Scribe_Collections.Look(ref matchModeList, "matchModeList", LookMode.Value);

                if (keys != null && canBeExtractedList != null && canBeMatchedList != null)
                {
                    extendedProps.Clear();
                    for (int i = 0; i < keys.Count && i < canBeExtractedList.Count && i < canBeMatchedList.Count; i++)
                    {
                        extendedProps[keys[i]] = new ExtendedProperties
                        {
                            canBeExtracted = canBeExtractedList[i],
                            canBeMatched = canBeMatchedList[i],
                            matchMode = (matchModeList != null && i < matchModeList.Count) ? matchModeList[i] : KeywordMatchMode.Any
                        };
                    }
                }
            }
        }
    }
}
