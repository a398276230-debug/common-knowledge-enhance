using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace RimTalk_ExpandedPreview
{
    public class Dialog_StopWords : Window
    {
        private string startWordsBuffer;
        private string endWordsBuffer;
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public Dialog_StopWords()
        {
            this.forcePause = true;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;

            // 从设置加载初始值，并进行null检查
            if (RimTalk_ExpandedPreviewMod.Settings.startStopWords == null)
            {
                RimTalk_ExpandedPreviewMod.Settings.startStopWords = new List<string>();
            }
            if (RimTalk_ExpandedPreviewMod.Settings.endStopWords == null)
            {
                RimTalk_ExpandedPreviewMod.Settings.endStopWords = new List<string>();
            }
            
            startWordsBuffer = string.Join("\n", RimTalk_ExpandedPreviewMod.Settings.startStopWords);
            endWordsBuffer = string.Join("\n", RimTalk_ExpandedPreviewMod.Settings.endStopWords);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("RTExpPrev_StopWords_Title".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            GUI.color = Color.gray;
            listing.Label("RTExpPrev_StopWords_Description".Translate());
            GUI.color = Color.white;
            listing.Gap(12f);

            Rect twoColumnRect = listing.GetRect(300f);
            float columnWidth = (twoColumnRect.width - 10f) / 2f;

            // 左列：开头停用词
            Rect leftRect = new Rect(twoColumnRect.x, twoColumnRect.y, columnWidth, twoColumnRect.height);
            Widgets.Label(leftRect, "RTExpPrev_StopWords_StartWords".Translate());
            Rect leftArea = new Rect(leftRect.x, leftRect.y + 30f, leftRect.width, leftRect.height - 30f);
            startWordsBuffer = Widgets.TextArea(leftArea, startWordsBuffer);

            // 右列：结尾停用词
            Rect rightRect = new Rect(twoColumnRect.x + columnWidth + 10f, twoColumnRect.y, columnWidth, twoColumnRect.height);
            Widgets.Label(rightRect, "RTExpPrev_StopWords_EndWords".Translate());
            Rect rightArea = new Rect(rightRect.x, rightRect.y + 30f, rightRect.width, rightRect.height - 30f);
            endWordsBuffer = Widgets.TextArea(rightArea, endWordsBuffer);

            listing.End();
        }

        public override void PreClose()
        {
            base.PreClose();
            // 保存设置
            RimTalk_ExpandedPreviewMod.Settings.startStopWords = startWordsBuffer.Split('\n').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            RimTalk_ExpandedPreviewMod.Settings.endStopWords = endWordsBuffer.Split('\n').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            RimTalk_ExpandedPreviewMod.Settings.Write();
        }
    }
}
