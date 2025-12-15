// 文件名: Dialog_SettingsUI.cs
// 文件功能：提供MECP设置功能的UI对话框。

using UnityEngine;
using Verse;

namespace RimTalk_ExpandedPreview
{
    public class Dialog_SettingsUI : Window
    {
        // 构造函数，设置窗口的基本属性
        public Dialog_SettingsUI()
        {
            this.forcePause = true;         // 打开窗口时暂停游戏
            this.doCloseButton = true;      // 显示右上角的关闭按钮
            this.doCloseX = true;           // 显示 "X" 关闭按钮
            this.closeOnClickedOutside = true; // 点击窗口外关闭
        }

        // 设置窗口的初始大小
        public override Vector2 InitialSize => new Vector2(800f, 700f);

        // 绘制窗口内容的核心方法
        public override void DoWindowContents(Rect inRect)
        {
            RimTalk_ExpandedPreviewMod.Settings.DoWindowContents(inRect);
        }

        // 当窗口关闭时调用，用来保存设置
        public override void PostClose()
        {
            base.PostClose();
            RimTalk_ExpandedPreviewMod.Settings.Write();
            Log.Message("[RimTalk_ExpandedPreview] Settings saved.");
        }
    }
}
