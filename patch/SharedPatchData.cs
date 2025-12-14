using System.Collections.Generic;

namespace RimTalk_ExpandedPreview
{
    public static class SharedPatchData
    {
        // 静态属性，用于存储从常识内容中提取的关键词
        public static List<string> ExtractedKnowledgeContentKeywords { get; set; } = new List<string>();
    }
}
