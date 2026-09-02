// ⚠ The look-alike that is genuinely correct, and the reason for the same-name guard. A
// descending comparer delegating to an ascending one is the crosswise shape exactly, and
// swapping it back is the one edit that would break it. The call is to a method of the enclosing
// member's own name, which is what a reversal-by-delegation looks like every time.
namespace Fixtures {
    sealed class Descending {
        readonly System.Collections.Generic.IComparer<string> inner =
            System.StringComparer.Ordinal;

        public int Compare(string left, string right) => inner.Compare(right, left);
    }
}
