using HarmonyLib;
using RimTalk.Memory;
using RimTalk.MemoryPatch; // 添加：自定义关键词设置所在的命名空间
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk_ExpandedPreview
{
    // 文件功能：处理中文关键词的Harmony补丁。
    [HarmonyPatch]
    internal static class ChineseKeywordPatch
    {
        // 目标：RimTalk.Memory.SuperKeywordEngine 中的私有静态方法 "ContainsChinese"
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(RimTalk.Memory.SuperKeywordEngine), "ContainsChinese");
        }

        // 后缀补丁：用更严格的检查覆盖原始结果，仅当字符串全部为中文字符时返回 true
        static void Postfix(string text, ref bool __result)
        {
            __result = IsAllChinese(text);
        }

        private static bool IsAllChinese(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                // 中文字符 Unicode 范围：\u4e00 - \u9fa5
                if (c < 0x4e00 || c > 0x9fa5)
                    return false;
            }

            return true;
        }
    }
}
