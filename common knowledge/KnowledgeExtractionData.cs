using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld; // ⭐ 添加 RimWorld 命名空间
using RimWorld.Planet;

namespace RimTalk_ExpandedPreview
{
    public class KnowledgeExtractionData : WorldComponent
    {
        // 开关1：是否允许内容匹配
        private static Dictionary<string, bool> _allowContentMatching = new Dictionary<string, bool>();
        // 开关2：是否允许提取关键词
        private static Dictionary<string, bool> _allowKeywordExtraction = new Dictionary<string, bool>();

        public KnowledgeExtractionData(World world) : base(world) { }

        // --- 内容匹配控制 ---
        public static bool IsContentMatchingAllowed(string entryId)
        {
            if (!_allowContentMatching.ContainsKey(entryId)) return false; // 默认禁止
            return _allowContentMatching[entryId];
        }
        public static void SetContentMatchingAllowed(string entryId, bool isAllowed) => _allowContentMatching[entryId] = isAllowed;
        public static void ToggleContentMatching(string entryId) => SetContentMatchingAllowed(entryId, !IsContentMatchingAllowed(entryId));

        // --- 关键词提取控制 ---
        public static bool IsKeywordExtractionAllowed(string entryId)
        {
            if (!_allowKeywordExtraction.ContainsKey(entryId)) return false; // 默认禁止
            return _allowKeywordExtraction[entryId];
        }
        public static void SetKeywordExtractionAllowed(string entryId, bool isAllowed) => _allowKeywordExtraction[entryId] = isAllowed;
        public static void ToggleKeywordExtraction(string entryId) => SetKeywordExtractionAllowed(entryId, !IsKeywordExtractionAllowed(entryId));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _allowContentMatching, "allowContentMatchingFlags", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _allowKeywordExtraction, "allowKeywordExtractionFlags", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_allowContentMatching == null) _allowContentMatching = new Dictionary<string, bool>();
                if (_allowKeywordExtraction == null) _allowKeywordExtraction = new Dictionary<string, bool>();
            }
        }
        
        public static void Cleanup(List<string> validIds)
        {
            if (_allowContentMatching != null && _allowContentMatching.Any())
            {
                var keysToRemove = _allowContentMatching.Keys.Except(validIds).ToList();
                foreach (var key in keysToRemove) _allowContentMatching.Remove(key);
            }
            if (_allowKeywordExtraction != null && _allowKeywordExtraction.Any())
            {
                var keysToRemove = _allowKeywordExtraction.Keys.Except(validIds).ToList();
                foreach (var key in keysToRemove) _allowKeywordExtraction.Remove(key);
            }
        }
    }
}
