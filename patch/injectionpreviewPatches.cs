using HarmonyLib;
using RimTalk.Memory;
using RimTalk.Memory.Debug;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;
using RimTalk.MemoryPatch; // 引用原版Settings类

namespace RimTalk_ExpandedPreview
{
    // 这个文件将不再替换 Dialog_InjectionPreview 的构造函数，
    // 而是通过 Postfix 补丁在原版 UI 上添加内容。
    // 因此，原有的 Transpiler 补丁将被移除。

    // 新增一个静态类来存放扩展 UI 的逻辑和状态
    public static class ExpandedPreviewUI
    {
        // 状态字段，需要是静态的，因为 Harmony 补丁是静态的
        public static PawnKeywordInfo lastTargetPawnKeywordInfo;
        public static List<KnowledgeScoreDetail> allKnowledgeScoreDetails; // 存储所有常识的详细评分
        public static KeywordExtractionInfo lastKeywordInfo; // 存储关键词提取信息
        public static Vector2 keywordScrollPosition;
        public static Vector2 knowledgeScoreScrollPosition;
        public static bool showPawnKeywords = false; // 用于切换显示Pawn关键词的详细分类
        public static bool showAllKnowledgeScores = false; // 用于切换显示所有常识（包括未通过阈值的）

        // 辅助方法，从 Dialog_ExpandedInjectionPreviewNew.cs 迁移过来
        public static void DrawDetailedAnalysis(Rect rect, Pawn selectedPawn, Pawn targetPawn, string contextInput)
        {
            // 背景
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            Rect innerRect = rect.ContractedBy(5f);
            float currentY = innerRect.y;

            // 常识评分详情
            Text.Font = GameFont.Medium;
            GUI.color = new Color(0.8f, 1f, 0.8f);
            Widgets.Label(new Rect(innerRect.x, currentY, innerRect.width, 30f), "RTExpPrev_Preview_KnowledgeScoringHeader".Translate()); // 🎓 常识评分详情
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            currentY += 35f;

            // 常识评分区域占据整个剩余高度
            Rect knowledgeScoreArea = new Rect(innerRect.x, currentY, innerRect.width, innerRect.height - (currentY - innerRect.y));
            Widgets.DrawBoxSolid(knowledgeScoreArea, new Color(0.15f, 0.15f, 0.15f, 0.5f)); // 常识评分区域背景

            DrawKnowledgeScoring(knowledgeScoreArea.ContractedBy(5f));
        }


        public static void DrawKnowledgeContentKeywords(Rect rect)
        {
            // 背景
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            Rect innerRect = rect.ContractedBy(5f);
            float currentY = innerRect.y;

            // 标题
            Text.Font = GameFont.Medium;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(new Rect(innerRect.x, currentY, innerRect.width, 30f), "RTExpPrev_Preview_KnowledgeContentKeywordsHeader".Translate()); // 📚 【常识内容关键词列表】
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            currentY += 35f;

            // 关键词区域
            Rect keywordArea = new Rect(innerRect.x, currentY, innerRect.width, innerRect.height - (currentY - innerRect.y));
            Widgets.DrawBoxSolid(keywordArea, new Color(0.15f, 0.15f, 0.15f, 0.5f));

            // 获取关键词
            List<string> knowledgeContentKeywords = SharedPatchData.ExtractedKnowledgeContentKeywords;

            if (knowledgeContentKeywords != null && knowledgeContentKeywords.Any())
            {
                StringBuilder sb = new StringBuilder();
                var grouped = knowledgeContentKeywords
                    .GroupBy(kw => kw.Length)
                    .OrderByDescending(g => g.Key);

                foreach (var group in grouped)
                {
                    sb.AppendLine("RTExpPrev_Preview_KeywordGroupHeader".Translate(group.Key, group.Count())); // 【{0}字关键词】 ({1}个):
                    var keywords = group.OrderBy(kw => kw).Take(20).ToList();
                    sb.AppendLine("  " + string.Join(", ", keywords));
                    if (group.Count() > 20)
                    {
                        sb.AppendLine("RTExpPrev_Preview_MoreKeywords".Translate(group.Count() - 20)); // ... 还有 {0} 个
                    }
                    sb.AppendLine();
                }

                float contentHeight = Text.CalcHeight(sb.ToString(), keywordArea.width - 20f);
                Rect viewRect = new Rect(0f, 0f, keywordArea.width - 20f, contentHeight + 50f);

                Widgets.BeginScrollView(keywordArea.ContractedBy(5f), ref keywordScrollPosition, viewRect);
                Widgets.Label(new Rect(0f, 0f, viewRect.width, contentHeight), sb.ToString());
                Widgets.EndScrollView();
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(keywordArea.ContractedBy(5f), "RTExpPrev_Preview_NoKnowledgeContentKeywords".Translate()); // 无常识内容关键词
                GUI.color = Color.white;
            }
        }

        // 关键词提取详情内容已废弃，因此移除 DrawKeywordExtraction 和 AppendKeywords 方法
        // public static void DrawKeywordExtraction(...) { ... }
        // public static void AppendKeywords(...) { ... }

        public static void DrawKnowledgeScoring(Rect rect)
        {
            if (allKnowledgeScoreDetails == null)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect, "RTExpPrev_Preview_ClickRefreshKnowledgeScores".Translate()); // 点击 '刷新预览' 获取常识评分数据.
                GUI.color = Color.white;
                return;
            }

            float currentY = rect.y;

            // 阈值信息
            var settings = RimTalkMemoryPatchMod.Settings;
            float threshold = settings?.knowledgeScoreThreshold ?? 0.1f;
            GUI.color = new Color(0.9f, 0.9f, 0.7f);
            Widgets.Label(new Rect(rect.x, currentY, rect.width, Text.LineHeight),
                "RTExpPrev_Preview_KnowledgeInjectionThreshold".Translate(threshold.ToString("F2"))); // 常识注入阈值: {0}
            GUI.color = Color.white;
            currentY += Text.LineHeight;

            Rect toggleRect = new Rect(rect.x, currentY, rect.width * 0.8f, 24f);
            Widgets.CheckboxLabeled(toggleRect, "RTExpPrev_Preview_ShowAllKnowledgeToggle".Translate(), ref showAllKnowledgeScores); // 显示所有常识 (包括未通过阈值的)
            currentY += 30f;

            Rect scrollArea = new Rect(rect.x, currentY, rect.width, rect.height - (currentY - rect.y));

            StringBuilder sb = new StringBuilder();
            int index = 1;
            foreach (var detail in allKnowledgeScoreDetails)
            {
                if (!showAllKnowledgeScores && detail.TotalScore < threshold) continue;

                // 使用颜色区分是否通过阈值
                if (detail.TotalScore >= threshold)
                {
                    sb.Append("✅ ");
                    sb.Append("<color=#8aff8a>"); // 绿色
                }
                else
                {
                    sb.Append("❌ ");
                    sb.Append("<color=#ff8a8a>"); // 红色
                }

                sb.AppendLine("RTExpPrev_Preview_TotalScore".Translate(index, detail.TotalScore.ToString("F3"))); // [{0}] 总分: {1}
                sb.AppendLine("    " + "RTExpPrev_Preview_TagContent".Translate(detail.Entry.tag, detail.Entry.content.Truncate(80))); // 标签: {0} | 内容: {1}
                sb.AppendLine("    " + "RTExpPrev_Preview_BaseImportanceScore".Translate(detail.ImportanceScore.ToString("F3"))); // ├─ 基础重要性分: {0}
                sb.AppendLine("    " + "RTExpPrev_Preview_TagMatchScore".Translate(detail.TagScore.ToString("F3"), string.Join(", ", detail.MatchedTags))); // ├─ 标签匹配分: {0} ({1})

                // 显示匹配关键词（最多3个），优先使用 detail.MatchedKeywords（来自 CommonKnowledgeLibrary）回退到反射方法
                string matchedPreview = string.Empty;
                try
                {
                    if (detail.MatchedKeywords != null && detail.MatchedKeywords.Any())
                    {
                        var top = detail.MatchedKeywords.Take(3).ToList();
                        string more = detail.MatchedKeywords.Count > 3 ? "RTExpPrev_Preview_MoreMatchKeywords".Translate(detail.MatchedKeywords.Count - 3).ToString() : string.Empty; // ...({0} more)
                        matchedPreview = "RTExpPrev_Preview_MatchedKeywordsList".Translate(string.Join(", ", top), more); // (匹配: {0}{1})
                    }
                    else
                    {
                        matchedPreview = GetMatchedKeywordsPreview(detail, 3);
                    }
                }
                catch { matchedPreview = GetMatchedKeywordsPreview(detail, 3); }


                sb.AppendLine("    " + "RTExpPrev_Preview_KeywordContentScore".Translate(detail.KeywordMatchCount, matchedPreview)); // ├─ 关键词内容分: {0}个关键词匹配 {1}
                sb.AppendLine("    " + "RTExpPrev_Preview_ExactMatchBonus".Translate(detail.JaccardScore.ToString("F3"))); // ├─ 精确匹配加成: {0}
                sb.AppendLine("    " + "RTExpPrev_Preview_Status".Translate(detail.FailReason)); // └─ 状态: {0}
                sb.AppendLine($"</color>");
                sb.AppendLine();
                index++;
            }

            float contentHeight = Text.CalcHeight(sb.ToString(), scrollArea.width - 20f);
            Rect viewRect = new Rect(0f, 0f, scrollArea.width - 20f, contentHeight + 50f);

            Widgets.BeginScrollView(scrollArea, ref knowledgeScoreScrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, contentHeight), sb.ToString());
            Widgets.EndScrollView();
        }

        // 通过反射尝试获取 KnowledgeScoreDetail 中记录的匹配关键词集合，返回最多 max 个并格式化
        public static string GetMatchedKeywordsPreview(KnowledgeScoreDetail detail, int max)
        {
            if (detail == null) return string.Empty;

            try
            {
                var type = detail.GetType();

                // 优先寻找属性名中包含 "keyword" 的 IEnumerable<string>
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var p in props)
                {
                    var name = p.Name.ToLowerInvariant();
                    if (!name.Contains("keyword") && !name.Contains("matched")) continue;
                    var value = p.GetValue(detail) as System.Collections.IEnumerable;
                    if (value == null) continue;
                    var list = new List<string>();
                    foreach (var o in value)
                    {
                        if (o == null) continue;
                        list.Add(o.ToString());
                    }
                    if (list.Count > 0)
                    {
                        var display = list.Take(max);
                        string more = list.Count > max ? "RTExpPrev_Preview_MoreMatchKeywords".Translate(list.Count - max).ToString() : string.Empty; // ...({0} more)
                        return "RTExpPrev_Preview_MatchedKeywordsList".Translate(string.Join(", ", display), more); // (匹配: {0}{1})
                    }
                }

                // 如果没有找到关键字集合，尝试查找包含 "match" 的属性
                foreach (var p in props)
                {
                    var name = p.Name.ToLowerInvariant();
                    if (!name.Contains("match")) continue;
                    var value = p.GetValue(detail) as System.Collections.IEnumerable;
                    if (value == null) continue;
                    var list = new List<string>();
                    foreach (var o in value)
                    {
                        if (o == null) continue;
                        list.Add(o.ToString());
                    }
                    if (list.Count > 0)
                    {
                        var display = list.Take(max);
                        string more = list.Count > max ? "RTExpPrev_Preview_MoreMatchKeywords".Translate(list.Count - max).ToString() : string.Empty; // ...({0} more)
                        return "RTExpPrev_Preview_MatchedKeywordsList".Translate(string.Join(", ", display), more); // (匹配: {0}{1})
                    }
                }

                // 回退：使用 MatchedTags（如果有）
                var tagsProp = props.FirstOrDefault(p => p.Name == "MatchedTags");
                if (tagsProp != null)
                {
                    var value = tagsProp.GetValue(detail) as System.Collections.IEnumerable;
                    if (value != null)
                    {
                        var list = new List<string>();
                        foreach (var o in value) if (o != null) list.Add(o.ToString());
                        if (list.Count > 0)
                        {
                            var display = list.Take(max);
                            string more = list.Count > max ? "RTExpPrev_Preview_MoreMatchKeywords".Translate(list.Count - max).ToString() : string.Empty; // ...({0} more)
                            return "RTExpPrev_Preview_MatchedTagsList".Translate(string.Join(", ", display), more); // (匹配标签: {0}{1})
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }
    }

    // Harmony 补丁类，用于在原版 Dialog_InjectionPreview.DoWindowContents 上添加内容
    [HarmonyPatch(typeof(Dialog_InjectionPreview), nameof(Dialog_InjectionPreview.DoWindowContents))]
    public static class Patch_DialogInjectionPreview_DoWindowContents
    {
        [HarmonyPrefix]
        public static void Prefix(ref Rect inRect, out Rect __state)
        {
            // Store the original full-size rect to use in Postfix
            __state = inRect;
            // Halve the width for the original DoWindowContents method to draw in.
            // This prevents its scrollbar from overlapping our new UI.
            inRect.width *= 0.5f;
        }

        [HarmonyPostfix]
        public static void Postfix(Dialog_InjectionPreview __instance, Rect __state)
        {
            Rect inRect = __state;

            // 获取原版 Dialog_InjectionPreview 的私有字段
            Pawn selectedPawn = (Pawn)AccessTools.Field(typeof(Dialog_InjectionPreview), "selectedPawn").GetValue(__instance);
            Pawn targetPawn = (Pawn)AccessTools.Field(typeof(Dialog_InjectionPreview), "targetPawn").GetValue(__instance);
            string contextInput = (string)AccessTools.Field(typeof(Dialog_InjectionPreview), "contextInput").GetValue(__instance);

            // 计算右侧布局
            float leftColumnWidth = inRect.width * 0.5f;
            float rightColumnX = leftColumnWidth + 10f;
            float rightColumnWidth = inRect.width - rightColumnX - 10f;

            // ⭐ 数据现在由下面的 CommonKnowledgeLibrary_InjectKnowledgeWithDetails_Postfix 补丁自动填充
            // 不再需要手动调用，以避免重复计算和性能问题。
            if (selectedPawn == null)
            {
                // 如果没有选中Pawn，确保清除旧数据
                ExpandedPreviewUI.allKnowledgeScoreDetails = null;
                ExpandedPreviewUI.lastKeywordInfo = null;
            }

            // 绘制两个关键词按钮（在右上角）
            float kbBtnWidth = 140f;
            float kbBtnHeight = 30f;
            float kbSpacing = 8f;
            float topY = 5f; // 从顶部开始
            
            Rect globalBtnRect = new Rect(rightColumnX, topY, kbBtnWidth, kbBtnHeight);
            if (Widgets.ButtonText(globalBtnRect, "RTExpPrev_Preview_GlobalKeywordsButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SettingsUI());
            }

            Rect saveBtnRect = new Rect(globalBtnRect.xMax + kbSpacing, topY, kbBtnWidth, kbBtnHeight);
            bool inGame = Current.Game != null;
            bool prevEnabled = GUI.enabled;
            GUI.enabled = inGame;
            if (Widgets.ButtonText(saveBtnRect, "RTExpPrev_Preview_SaveKeywordsButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SaveGameKeywords());
            }
            GUI.enabled = prevEnabled;

            // 计算面板位置（从按钮下方开始到窗口底部）
            float panelsY = topY + kbBtnHeight + 10f;
            float panelsTotalHeight = inRect.height - panelsY - 10f;

            // 分割右侧列的高度
            float keywordPanelHeight = panelsTotalHeight * 0.4f;
            float scoringPanelHeight = panelsTotalHeight * 0.6f;

            // 定义两个面板的 Rect
            Rect keywordPanelRect = new Rect(rightColumnX, panelsY, rightColumnWidth, keywordPanelHeight - 5f);
            Rect scoringPanelRect = new Rect(rightColumnX, keywordPanelRect.yMax + 10f, rightColumnWidth, scoringPanelHeight - 5f);

            // 绘制右上方面板（常识内容关键词）
            ExpandedPreviewUI.DrawKnowledgeContentKeywords(keywordPanelRect);

            // 绘制右下方面板（常识评分详情）
            ExpandedPreviewUI.DrawDetailedAnalysis(scoringPanelRect, selectedPawn, targetPawn, contextInput);
        }
    }

    // Harmony 补丁类，用于修改 Dialog_InjectionPreview 的 InitialSize
    [HarmonyPatch(typeof(Dialog_InjectionPreview), "get_InitialSize")]
    public static class Patch_DialogInjectionPreview_InitialSize
    {
        [HarmonyPostfix]
        public static void Postfix(ref Vector2 __result)
        {
            // 调整宽度以适应两列布局
            __result.x *= 1.5f;
            // 适当增加高度，以适应更多的信息
            __result.y *= 1.2f; // 增加20%的高度
        }
    }

    // =========================================================================
    // ⭐ 新增补丁：捕获 InjectKnowledgeWithDetails 的结果
    // =========================================================================
    // 目标：CommonKnowledgeLibrary.InjectKnowledgeWithDetails
    // 职责：在原版UI调用此方法后，捕获其输出结果并存入静态字段，
    //       供我们的UI扩展部分使用，避免重复计算。
    [HarmonyPatch]
    public static class CommonKnowledgeLibrary_InjectKnowledgeWithDetails_CapturePatch
    {
        // 使用 TargetMethod 来动态定位带有 out 参数的复杂方法签名
        static MethodBase TargetMethod()
        {
            // 定义目标方法的参数类型数组
            var parameterTypes = new Type[]
            {
                typeof(string),
                typeof(int),
                typeof(List<KnowledgeScore>).MakeByRefType(), // out List<KnowledgeScore> scores
                typeof(List<KnowledgeScoreDetail>).MakeByRefType(), // out List<KnowledgeScoreDetail> allScores
                typeof(KeywordExtractionInfo).MakeByRefType(), // out KeywordExtractionInfo keywordInfo
                typeof(Pawn),
                typeof(Pawn)
            };
            
            // 使用参数类型精确查找方法
            return AccessTools.Method(typeof(CommonKnowledgeLibrary), "InjectKnowledgeWithDetails", parameterTypes);
        }

        [HarmonyPostfix]
        public static void Postfix(List<KnowledgeScoreDetail> allScores, KeywordExtractionInfo keywordInfo)
        {
            // 当原版UI调用InjectKnowledgeWithDetails时，这个Postfix会被触发
            // 我们在这里捕获结果，存到静态变量里
            ExpandedPreviewUI.allKnowledgeScoreDetails = allScores;
            ExpandedPreviewUI.lastKeywordInfo = keywordInfo;

            if (Prefs.DevMode)
            {
                Log.Message($"[RimTalk_ExpandedPreview] Captured {allScores?.Count ?? 0} knowledge score details and keyword info from the original call.");
            }
        }
    }
}
