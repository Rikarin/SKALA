// Both call forms have to keep compiling after the block goes in, so this fixture uses each one:
// `TextTools.Trimmed(s)` is the static form and `s.Padded(3)` is the extension form. The re-binding
// half of the fixture harness is what actually proves it — if C# 14 dropped the static form, the
// call below would stop compiling once the fix was applied and the test would say so.
namespace Fixtures {
    static class TextTools {
        internal static string Trimmed(this string text) => text.Trim();

        internal static string Padded(this string text, int width) => text.PadLeft(width);
    }

    static class TextToolsUses {
        internal static string Run(string s) => TextTools.Trimmed(s) + s.Padded(3);
    }
}
