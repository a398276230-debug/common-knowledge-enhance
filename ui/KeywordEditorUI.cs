using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace RimTalk_ExpandedPreview
{
    // 文件功能：提供关键词编辑功能的UI界面。
    public static class KeywordEditorUI
    {
        private static Texture2D CheckboxOn = null;
        private static Texture2D CheckboxOff = null;

        private static bool isDraggingCheckbox = false;
        private static bool? dragToggleTargetState = null; // true = on, false = off

        private static void EnsureCheckboxTexturesLoaded()
        {
            if (CheckboxOn != null && CheckboxOff != null) return;

            string[] onPaths = new[] { "UI/Checkboxes/checkbox_on" };
            string[] offPaths = new[] { "UI/Checkboxes/checkbox_off" };

            foreach (var p in onPaths)
            {
                var t = ContentFinder<Texture2D>.Get(p, false);
                if (t != null)
                {
                    CheckboxOn = t;
                    break;
                }
            }
            foreach (var p in offPaths)
            {
                var t = ContentFinder<Texture2D>.Get(p, false);
                if (t != null)
                {
                    CheckboxOff = t;
                    break;
                }
            }

            if (CheckboxOn == null) CheckboxOn = BaseContent.ClearTex;
            if (CheckboxOff == null) CheckboxOff = BaseContent.ClearTex;
        }

        public static bool DoDraggableCheckbox(Rect rect, ref bool checkOn)
        {
            bool changed = false;
            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                isDraggingCheckbox = true;
                checkOn = !checkOn;
                changed = true;
                dragToggleTargetState = checkOn;
                ev.Use();
            }
            else if (isDraggingCheckbox && dragToggleTargetState.HasValue && rect.Contains(ev.mousePosition) && checkOn != dragToggleTargetState.Value)
            {
                checkOn = dragToggleTargetState.Value;
                changed = true;
                ev.Use();
            }

            EnsureCheckboxTexturesLoaded();

            Texture2D tex = checkOn ? CheckboxOn : CheckboxOff;
            if (tex != null)
            {
                GUI.DrawTexture(rect, tex);
            }
            else
            {
                bool prevEnabled = GUI.enabled;
                GUI.enabled = false;
                GUI.Toggle(rect, checkOn, "");
                GUI.enabled = prevEnabled;
            }

            return changed;
        }

        private static float GetViewRectHeight(int count)
        {
            return 24f + (count * 30f) + 10f;
        }

        public static void DrawKeywordTable(Rect outerRect, ref Vector2 scrollPosition, ref string searchTextRef, List<CustomKeywordEntry> entries)
        {
            if (entries == null) return;

            string searchText = searchTextRef ?? string.Empty;

            // compute matchedCount without LINQ to avoid capturing ref
            int matchedCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (string.IsNullOrEmpty(searchText) || ((e.keyword ?? "").IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                    matchedCount++;
            }

            float availableWidth = outerRect.width - 30f - 24f;
            if (availableWidth < 200f) availableWidth = Math.Max(outerRect.width * 0.5f, 200f);
            float keywordColWidth = availableWidth * 0.5f;
            float checkboxColWidth = availableWidth * 0.125f;

            Rect viewRect = new Rect(0f, 0f, outerRect.width - 16f, GetViewRectHeight(matchedCount));

            Widgets.BeginScrollView(outerRect, ref scrollPosition, viewRect);

            // header
            Rect headerRect = new Rect(viewRect.x, 0, viewRect.width, 24f);
            float currentX = headerRect.x + 30f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(currentX, headerRect.y, keywordColWidth, headerRect.height), "RTExpPrev_KeywordEditor_KeywordHeader".Translate());
            currentX += keywordColWidth;
            Widgets.Label(new Rect(currentX, headerRect.y, checkboxColWidth, headerRect.height), "RTExpPrev_KeywordEditor_WholeWordHeader".Translate());
            currentX += checkboxColWidth;
            Widgets.Label(new Rect(currentX, headerRect.y, checkboxColWidth, headerRect.height), "RTExpPrev_Settings_CaseSensitiveColumn".Translate());
            currentX += checkboxColWidth;
            Widgets.Label(new Rect(currentX, headerRect.y, checkboxColWidth, headerRect.height), "RTExpPrev_Settings_NegativeColumn".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float currentY = headerRect.yMax;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!string.IsNullOrEmpty(searchText) && ((entry.keyword ?? "").IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                Rect rowRect = new Rect(viewRect.x, currentY, viewRect.width, 30f);
                currentY += 30f;
                currentX = rowRect.x;

                Rect isEnabledRect = new Rect(currentX, rowRect.y + 4f, 24f, 24f);
                if (DoDraggableCheckbox(isEnabledRect, ref entry.isEnabled)) { GUI.changed = true; }
                currentX += 30f;

                Rect keywordRect = new Rect(currentX, rowRect.y + 2f, keywordColWidth, 28f);
                entry.keyword = Widgets.TextField(keywordRect, entry.keyword ?? "");
                currentX += keywordColWidth;

                Rect wholeWordRect = new Rect(currentX + (checkboxColWidth / 2f) - 12f, rowRect.y + 4f, 24f, 24f);
                if (DoDraggableCheckbox(wholeWordRect, ref entry.matchWholeWord)) { GUI.changed = true; }
                currentX += checkboxColWidth;

                Rect caseSensitiveRect = new Rect(currentX + (checkboxColWidth / 2f) - 12f, rowRect.y + 4f, 24f, 24f);
                if (DoDraggableCheckbox(caseSensitiveRect, ref entry.caseSensitive)) { GUI.changed = true; }
                currentX += checkboxColWidth;

                Rect negativeRect = new Rect(currentX + (checkboxColWidth / 2f) - 12f, rowRect.y + 4f, 24f, 24f);
                if (DoDraggableCheckbox(negativeRect, ref entry.isNegative)) { GUI.changed = true; }

                Rect deleteButtonRect = new Rect(rowRect.xMax - 24f, rowRect.y + 4f, 24f, 24f);
                if (Widgets.ButtonImage(deleteButtonRect, TexButton.Delete))
                {
                    entries.RemoveAt(i);
                    i--;
                }
            }

            Widgets.EndScrollView();

            // reset dragging on mouse up
            if (isDraggingCheckbox && Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                isDraggingCheckbox = false;
                dragToggleTargetState = null;
                Event.current.Use();
            }

            // copy back any change to searchText if needed
            searchTextRef = searchText;
        }
    }
}
