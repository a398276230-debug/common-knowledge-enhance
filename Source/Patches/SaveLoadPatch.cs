using HarmonyLib;
using Verse;
using RimTalk.Memory;
using RimTalk.CommonKnowledgeEnhance;

namespace RimTalk.CommonKnowledgeEnhance.Patches
{
    /// <summary>
    /// 确保扩展属性能够被保存和加载
    /// </summary>
    [HarmonyPatch(typeof(Game), "ExposeData")]
    public static class GameExposeDataPatch
    {
        /// <summary>
        /// 后缀补丁：在游戏保存/加载时处理扩展属性
        /// </summary>
        static void Postfix()
        {
            // 保存/加载扩展属性
            ExtendedKnowledgeEntry.ExposeData();
        }
    }

    /// <summary>
    /// 在常识库删除条目时清理扩展属性
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "RemoveEntry")]
    public static class RemoveEntryPatch
    {
        /// <summary>
        /// 后缀补丁：删除条目后清理扩展属性
        /// </summary>
        static void Postfix(CommonKnowledgeLibrary __instance)
        {
            ExtendedKnowledgeEntry.CleanupDeletedEntries(__instance);
        }
    }

    /// <summary>
    /// 在常识库清空时清理扩展属性
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "Clear")]
    public static class ClearLibraryPatch
    {
        /// <summary>
        /// 后缀补丁：清空常识库后清理扩展属性
        /// </summary>
        static void Postfix(CommonKnowledgeLibrary __instance)
        {
            ExtendedKnowledgeEntry.CleanupDeletedEntries(__instance);
        }
    }
}
