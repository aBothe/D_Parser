using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Misc;

namespace Tests.Completion
{
    public class TestsEditorData : EditorData
    {
        public MutableRootPackage MainPackage => (ParseCache as LegacyParseCacheView).FirstPackage();
    }
}