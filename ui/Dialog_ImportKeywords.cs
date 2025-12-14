// File: Dialog_ImportKeywords.cs
// 文件功能：用于导入关键词的UI对话框。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk_ExpandedPreview
{
    public class Dialog_ImportKeywords : Window
    {
        private string inputText = "";
        private readonly Action<List<string>> onConfirm;
        private bool focused = false;

        public override Vector2 InitialSize => new Vector2(600f, 400f);

        public Dialog_ImportKeywords(Action<List<string>> onConfirmAction)
        {
            this.onConfirm = onConfirmAction;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
            this.closeOnCancel = true;
            // 注意：某些 RimWorld 版本的 Window 类没有 acceptAction 字段，故不使用该成员
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标题
            Text.Font = GameFont.Medium;
            listing.Label("RTExpPrev_Import_Title".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // 说明
            GUI.color = Color.gray;
            listing.Label("RTExpPrev_Import_Description".Translate());
            GUI.color = Color.white;
            listing.Gap(12f);

            // 计算剩余高度以确保底部按钮可见
            float remainingHeight = inRect.height - listing.CurHeight - 120f; // 留出空间给底部按钮和说明
            if (remainingHeight < 60f) remainingHeight = 60f;

            // 文本输入区域
            Rect textRect = listing.GetRect(remainingHeight);
            GUI.SetNextControlName("ImportTextField");
            inputText = Widgets.TextArea(textRect, inputText);

            if (!this.focused)
            {
                UI.FocusControl("ImportTextField", this);
                this.focused = true;
            }

            // 处理回车键：在文本框聚焦时按回车提交
            var ev = Event.current;
            if ((ev.type == EventType.KeyDown || ev.type == EventType.KeyUp) && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter))
            {
                if (GUI.GetNameOfFocusedControl() == "ImportTextField")
                {
                    ProcessAndImport();
                    ev.Use();
                }
            }

            listing.Gap(24f);

            // 底部按钮
            Rect bottomRect = listing.GetRect(35f);
            float buttonWidth = bottomRect.width / 2f - 10f;

            // 确认按钮
            Rect confirmRect = new Rect(bottomRect.x, bottomRect.y, buttonWidth, 35f);
            if (Widgets.ButtonText(confirmRect, "RTExpPrev_Import_ConfirmButton".Translate()))
            {
                ProcessAndImport();
            }

            // 取消按钮
            Rect cancelRect = new Rect(confirmRect.xMax + 20f, bottomRect.y, buttonWidth, 35f);
            if (Widgets.ButtonText(cancelRect, "RTExpPrev_Import_CancelButton".Translate()))
            {
                this.Close();
            }

            listing.End();
        }

        private void ProcessAndImport()
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                this.Close();
                return;
            }

            char[] delimiters = { ',', '，', '、', ';', ' ' };
            List<string> importedKeywords = inputText
                .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (importedKeywords.Any())
            {
                onConfirm?.Invoke(importedKeywords);
            }

            this.Close();
        }
    }
}
