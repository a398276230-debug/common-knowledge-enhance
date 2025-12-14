// 文件功能：处理主标签页的Harmony补丁。
// File: HarmonyPatches.cs (添加以下内容)
using HarmonyLib;
using RimTalk.Memory.Debug; // 引用原版InjectionPreview类
using RimTalk.Memory.UI;    // 引用原版MainTabWindow_Memory类
using RimTalk.MemoryPatch;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace RimTalk_ExpandedPreview
{
    // 这个文件将不再替换 Dialog_InjectionPreview 的构造函数，
    // 而是让原版 UI 正常工作。
    // 由于 injectionpreviewPatches.cs 已经处理了 Dialog_InjectionPreview.DoWindowContents 的 Postfix 补丁，
    // 这个文件不再需要额外的补丁。
}
