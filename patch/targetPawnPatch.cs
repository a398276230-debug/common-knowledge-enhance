using HarmonyLib;
using Verse;
using System.Reflection;
using System.Collections.Generic;
using RimTalk.Memory;
using System;

namespace RimTalk_ExpandedPreview
{
    // 只在类级别指定要修补的类型
    [HarmonyPatch(typeof(CommonKnowledgeLibrary))]
    public static class Patch_CommonKnowledgeLibrary
    {
        public static PawnKeywordInfo LastExtractedTargetPawnInfo = null;

        // ⭐ 新增：使用 TargetMethod() 来在运行时定位目标方法
        // Harmony 会自动调用这个方法来确定要 patch 哪个方法
        static MethodBase TargetMethod()
        {
            // 在这个方法内部，我们可以自由使用运行时才能确定的代码
            return AccessTools.Method(
                typeof(CommonKnowledgeLibrary),
                "InjectKnowledgeWithDetails",
                // 这里的 new Type[] 在运行时创建，是完全合法的
                new Type[] {
                    typeof(string),
                    typeof(int),
                    typeof(List<KnowledgeScore>).MakeByRefType(),
                    typeof(List<KnowledgeScoreDetail>).MakeByRefType(),
                    typeof(KeywordExtractionInfo).MakeByRefType(),
                    typeof(Pawn),
                    typeof(Pawn)
                }
            );
        }

        // Postfix 现在会自动应用到 TargetMethod() 返回的方法上
        [HarmonyPostfix]
        public static void InjectKnowledgeWithDetails_Postfix(
            CommonKnowledgeLibrary __instance,
            Pawn targetPawn)
        {
            if (targetPawn == null)
            {
                LastExtractedTargetPawnInfo = null;
                return;
            }

            try
            {
                MethodInfo extractMethod = typeof(CommonKnowledgeLibrary).GetMethod(
                    "ExtractPawnKeywordsWithDetails",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (extractMethod != null)
                {
                    object result = extractMethod.Invoke(__instance, new object[] { new List<string>(), targetPawn });
                    LastExtractedTargetPawnInfo = result as PawnKeywordInfo;
                }
                else
                {
                    Log.Warning("[RimTalk_ExpandedPreview] Harmony patch could not find private method 'ExtractPawnKeywordsWithDetails'. Target keyword info will not be available.");
                    LastExtractedTargetPawnInfo = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk_ExpandedPreview] Error in Harmony postfix for InjectKnowledgeWithDetails: {ex}");
                LastExtractedTargetPawnInfo = null;
            }
        }
    }
}
