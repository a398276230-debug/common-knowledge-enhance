// 文件功能：修复RimTalk标签的Harmony补丁。
// File: SaveEntryTagCacheFix.cs
using HarmonyLib;
using Verse;
using RimTalk.Memory.UI; // 引入 Dialog_CommonKnowledge 所在的命名空间
using RimTalk.Memory;     // 引入 CommonKnowledgeEntry 所在的命名空间
using System.Reflection;  // 用于反射访问私有字段

namespace RimTalk_ExpandedPreview // 确保命名空间与你的 Mod 项目一致
{
    [HarmonyPatch(typeof(Dialog_CommonKnowledge), "SaveEntry")]
    public static class SaveEntry_PostfixPatch
    {
        // Harmony 补丁方法的 __instance 参数通常是值传递的（除非加 ref）。
        // 要访问私有字段，你需要通过反射。
        [HarmonyPostfix]
        public static void Postfix(Dialog_CommonKnowledge __instance)
        {
            // **第一步：通过反射获取 Dialog_CommonKnowledge 实例的 selectedEntry 字段**
            // selectedEntry 是 private 字段，不能直接访问
            FieldInfo selectedEntryField = typeof(Dialog_CommonKnowledge)
                .GetField("selectedEntry", BindingFlags.Instance | BindingFlags.NonPublic);

            if (selectedEntryField == null)
            {
                Log.Warning("[RimTalk_ExpandedPreview] Could not find 'selectedEntry' field in Dialog_CommonKnowledge. " +
                            "RimTalk Mod might have updated, or the field name changed. This patch might be ineffective.");
                return;
            }

            // 获取当前 Dialog_CommonKnowledge 实例的 selectedEntry 字段的值
            CommonKnowledgeEntry entry = selectedEntryField.GetValue(__instance) as CommonKnowledgeEntry;

            // 确保有选中的常识条目
            if (entry != null)
            {
                // **第二步：通过反射获取 CommonKnowledgeEntry 类中的私有字段 'cachedTags'**
                FieldInfo cachedTagsField = typeof(CommonKnowledgeEntry)
                    .GetField("cachedTags", BindingFlags.Instance | BindingFlags.NonPublic);

                if (cachedTagsField != null)
                {
                    // 如果找到了字段，将其值设置为 null，从而清除缓存
                    cachedTagsField.SetValue(entry, null);
                    Log.Message($"[RimTalk_ExpandedPreview] Cleared cachedTags for CommonKnowledgeEntry ID: {entry.id}, Tag: '{entry.tag}'.");
                }
                else
                {
                    Log.Warning("[RimTalk_ExpandedPreview] Could not find 'cachedTags' field in CommonKnowledgeEntry. " +
                                "The RimTalk Mod might have updated, or the field name changed. This patch might be ineffective.");
                }
            }
            else
            {
                Log.Message("[RimTalk_ExpandedPreview] SaveEntry called, but no CommonKnowledgeEntry was selected/created. No cache to clear.");
            }
        }
    }
}
