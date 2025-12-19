using System;
using HarmonyLib;
using Verse;
using RimTalk.Memory;
using RimTalk.CommonKnowledgeEnhance;
using RimTalk.CommonKnowledgeEnhance.Vector;

namespace RimTalk.CommonKnowledgeEnhance.Patches
{
    /// <summary>
    /// 向量同步 Patch - 在增删改查常识时自动同步向量数据库
    /// </summary>
    
    /// <summary>
    /// Patch AddEntry 方法 - 添加条目时同步向量
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "AddEntry", new Type[] { typeof(CommonKnowledgeEntry) })]
    public static class Patch_AddEntry
    {
        static void Postfix(CommonKnowledgeEntry entry)
        {
            if (entry == null)
                return;

            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            if (!settings.enableVectorEnhancement)
                return;

            try
            {
                if (entry.isEnabled)
                {
                    VectorService.Instance.UpdateKnowledgeVector(entry.id, entry.content);
                    Log.Message($"[RimTalk-VectorSync] Vector updated for entry: {entry.id}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-VectorSync] Failed to sync vector on AddEntry: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch RemoveEntry 方法 - 删除条目时移除向量
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "RemoveEntry")]
    public static class Patch_RemoveEntry
    {
        static void Postfix(CommonKnowledgeEntry entry)
        {
            if (entry == null)
                return;

            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            if (!settings.enableVectorEnhancement)
                return;

            try
            {
                VectorService.Instance.RemoveKnowledgeVector(entry.id);
                Log.Message($"[RimTalk-VectorSync] Vector removed for entry: {entry.id}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-VectorSync] Failed to remove vector on RemoveEntry: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch Clear 方法 - 清空常识库时同步向量
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "Clear")]
    public static class Patch_Clear
    {
        static void Postfix(CommonKnowledgeLibrary __instance)
        {
            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            if (!settings.enableVectorEnhancement)
                return;

            try
            {
                VectorService.Instance.SyncKnowledgeLibrary(__instance);
                Log.Message("[RimTalk-VectorSync] Vector database cleared and synced");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-VectorSync] Failed to clear vectors on Clear: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch ImportFromText 方法 - 批量导入后同步向量
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "ImportFromText")]
    public static class Patch_ImportFromText
    {
        static void Postfix(CommonKnowledgeLibrary __instance, int __result)
        {
            if (__result == 0)
                return;

            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            if (!settings.enableVectorEnhancement)
                return;

            try
            {
                VectorService.Instance.SyncKnowledgeLibrary(__instance);
                Log.Message($"[RimTalk-VectorSync] Vector database synced after importing {__result} entries");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-VectorSync] Failed to sync vectors on ImportFromText: {ex.Message}");
            }
        }
    }
}
