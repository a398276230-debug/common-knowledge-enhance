using System.Collections.Generic;
using Verse;
using RimWorld.Planet;

namespace RimTalk_ExpandedPreview
{
    // 文件功能：处理游戏存档关键词的组件。
    public class SaveGameKeywordComponent : WorldComponent
    {
        public List<CustomKeywordEntry> saveKeywords = new List<CustomKeywordEntry>();

        public SaveGameKeywordComponent(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref saveKeywords, "saveKeywords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && saveKeywords == null)
            {
                saveKeywords = new List<CustomKeywordEntry>();
            }
        }

        public static SaveGameKeywordComponent ForCurrent()
        {
            if (Find.World == null) return null;
            return Find.World.GetComponent<SaveGameKeywordComponent>();
        }
    }
}
