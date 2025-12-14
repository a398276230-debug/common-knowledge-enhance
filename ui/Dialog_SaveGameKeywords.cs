using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimTalk_ExpandedPreview
{
    // 文件功能：用于保存游戏关键词的UI对话框。
    public class Dialog_SaveGameKeywords : Window
    {
        private Vector2 scroll = Vector2.zero;
        private string searchText = "";
        public Dialog_SaveGameKeywords()
        {
            this.forcePause = true;
            this.doCloseButton = true;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(800f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            var comp = SaveGameKeywordComponent.ForCurrent();
            if (comp == null)
            {
                Listing_Standard tmp = new Listing_Standard();
                tmp.Begin(inRect);
                tmp.Label("RTExpPrev_SaveGameKeywords_LoadSaveFirst".Translate());
                tmp.End();
                return;
            }

            // 简单复用 ExpandedPreviewSettings 的 UI 绘制：复制主要的关键词编辑部分，但绑定到 comp.saveKeywords
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("RTExpPrev_SaveGameKeywords_Title".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // 搜索栏
            Rect searchRect = listing.GetRect(30f);
            GUI.SetNextControlName("KeywordSearchSave");
            searchText = Widgets.TextField(searchRect, searchText ?? "");
            Rect clearRect = new Rect(searchRect.xMax - 26f, searchRect.y + 3f, 22f, 22f);
            if (Widgets.ButtonImage(clearRect, TexButton.Delete))
            {
                searchText = "";
            }

            listing.Gap(6f);

            // 使用通用 KeywordEditorUI
            Rect tableRect = listing.GetRect(inRect.height - listing.CurHeight - 120f);
            KeywordEditorUI.DrawKeywordTable(tableRect, ref scroll, ref searchText, comp.saveKeywords);

            listing.Gap(12f);

            // 底部按钮
            Rect buttonRowRect = listing.GetRect(35f);
            float halfWidth = buttonRowRect.width / 2f - 10f;
            Rect addButtonRect = new Rect(buttonRowRect.x, buttonRowRect.y, halfWidth, 35f);
            if (Widgets.ButtonText(addButtonRect, "RTExpPrev_SaveGameKeywords_AddNewKeyword".Translate()))
            {
                comp.saveKeywords.Add(new CustomKeywordEntry("", true, false, false, false));
            }
            Rect importButtonRect = new Rect(addButtonRect.xMax + 20f, buttonRowRect.y, halfWidth, 35f);
            if (Widgets.ButtonText(importButtonRect, "RTExpPrev_SaveGameKeywords_ImportButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ImportKeywords(list => {
                    if (list == null || !list.Any()) return;
                    foreach (var k in list)
                    {
                        bool exists = comp.saveKeywords.Any(e => string.Equals(e.keyword, k, System.StringComparison.OrdinalIgnoreCase));
                        if (!exists) comp.saveKeywords.Add(new CustomKeywordEntry(k, true, false, false, false));
                    }
                }));
            }

            listing.Gap(12f);
            GUI.color = new Color(0.7f, 0.9f, 1f);
            listing.Label("RTExpPrev_SaveGameKeywords_Explanation".Translate());
            GUI.color = Color.white;

            listing.End();

            // 窗口关闭时，WorldComponent 的 ExposeData 会在存档时自动保存。如果想立即持久化，需要用户保存游戏。
        }

        public override void PostClose()
        {
            base.PostClose();
            Messages.Message("RTExpPrev_SaveGameKeywords_SaveMessage".Translate(), MessageTypeDefOf.NeutralEvent);
        }
    }
}
