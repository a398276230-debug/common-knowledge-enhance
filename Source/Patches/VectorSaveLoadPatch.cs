using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using RimTalk.Memory;
using RimTalk.CommonKnowledgeEnhance;
using RimTalk.CommonKnowledgeEnhance.Vector;

namespace RimTalk.CommonKnowledgeEnhance.Patches
{
    /// <summary>
    /// 向量数据保存/加载 Patch - 在存档保存/加载时处理向量数据
    /// </summary>
    [HarmonyPatch(typeof(CommonKnowledgeLibrary), "ExposeData")]
    public static class Patch_ExposeData
    {
        // 使用静态字段临时存储向量数据（因为 Scribe 不支持嵌套列表）
        private static List<string> vectorIds;
        private static List<string> vectorDataSerialized;
        private static List<string> vectorHashes;

        static void Prefix(CommonKnowledgeLibrary __instance)
        {
            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            if (!settings.enableVectorEnhancement)
                return;

            // 保存时：导出向量数据
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    List<List<float>> vectorData;
                    VectorService.Instance.ExportVectorsForSave(
                        out vectorIds, out vectorData, out vectorHashes);
                    
                    // 将 List<List<float>> 转换为 List<string>（逗号分隔）
                    vectorDataSerialized = new List<string>();
                    if (vectorData != null)
                    {
                        foreach (var vector in vectorData)
                        {
                            if (vector != null && vector.Count > 0)
                            {
                                vectorDataSerialized.Add(string.Join(",", vector));
                            }
                            else
                            {
                                vectorDataSerialized.Add("");
                            }
                        }
                    }

                    Log.Message($"[RimTalk-VectorSaveLoad] Exported {vectorIds?.Count ?? 0} vectors for saving");
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-VectorSaveLoad] Failed to export vectors for save: {ex}");
                    vectorIds = null;
                    vectorDataSerialized = null;
                    vectorHashes = null;
                }
            }
        }

        static void Postfix(CommonKnowledgeLibrary __instance)
        {
            var settings = RimTalkCommonKnowledgeEnhance.Settings;
            if (!settings.enableVectorEnhancement)
                return;

            // 保存时：序列化向量数据
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Scribe_Collections.Look(ref vectorIds, "vectorIds", LookMode.Value);
                    Scribe_Collections.Look(ref vectorDataSerialized, "vectorDataSerialized", LookMode.Value);
                    Scribe_Collections.Look(ref vectorHashes, "vectorHashes", LookMode.Value);

                    Log.Message($"[RimTalk-VectorSaveLoad] Saved {vectorIds?.Count ?? 0} vectors to archive");
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-VectorSaveLoad] Failed to save vectors: {ex}");
                }
            }
            // 加载时：反序列化并恢复向量数据
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                try
                {
                    Scribe_Collections.Look(ref vectorIds, "vectorIds", LookMode.Value);
                    Scribe_Collections.Look(ref vectorDataSerialized, "vectorDataSerialized", LookMode.Value);
                    Scribe_Collections.Look(ref vectorHashes, "vectorHashes", LookMode.Value);

                    Log.Message($"[RimTalk-VectorSaveLoad] Loaded {vectorIds?.Count ?? 0} vectors from archive");
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-VectorSaveLoad] Failed to load vectors: {ex}");
                    vectorIds = null;
                    vectorDataSerialized = null;
                    vectorHashes = null;
                }
            }
            // 加载完成后：恢复向量数据并同步
            else if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                try
                {
                    // 先恢复向量数据（如果存在）
                    if (vectorIds != null && vectorDataSerialized != null && vectorHashes != null && vectorIds.Count > 0)
                    {
                        Log.Message($"[RimTalk-VectorSaveLoad] Restoring {vectorIds.Count} vectors from save...");
                        
                        // 将 List<string> 转换回 List<List<float>>
                        var vectorData = new List<List<float>>();
                        foreach (var serialized in vectorDataSerialized)
                        {
                            if (!string.IsNullOrEmpty(serialized))
                            {
                                var floats = new List<float>();
                                foreach (var str in serialized.Split(','))
                                {
                                    if (float.TryParse(str, out float value))
                                    {
                                        floats.Add(value);
                                    }
                                }
                                vectorData.Add(floats);
                            }
                            else
                            {
                                vectorData.Add(new List<float>());
                            }
                        }
                        
                        VectorService.Instance.ImportVectorsFromLoad(
                            vectorIds, vectorData, vectorHashes);
                        
                        Log.Message($"[RimTalk-VectorSaveLoad] Successfully restored {vectorIds.Count} vectors");
                    }
                    else
                    {
                        Log.Message("[RimTalk-VectorSaveLoad] No saved vectors found, will perform full sync.");
                    }
                    
                    // 再进行增量同步（只处理新增/修改的条目）
                    Log.Message("[RimTalk-VectorSaveLoad] Syncing knowledge library to vector database...");
                    VectorService.Instance.SyncKnowledgeLibrary(__instance);
                    
                    // 清理临时数据
                    vectorIds = null;
                    vectorDataSerialized = null;
                    vectorHashes = null;
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-VectorSaveLoad] Failed to restore/sync vectors on game load: {ex}");
                }
            }
        }
    }
}
