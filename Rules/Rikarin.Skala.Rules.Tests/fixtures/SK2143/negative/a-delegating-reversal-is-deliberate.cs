// ⚠ The look-alike that is genuinely correct, and the reason for the same-name guard. A
// descending comparer delegating to an ascending one is the crosswise shape exactly, and swapping
// it back is the one edit that would break it. The call is to a method of the enclosing member's
// own name, which is what a reversal-by-delegation looks like every time.
//
// ⚠ This fixture was written first against `IComparer<string>` and it proved nothing: the BCL
// names those parameters `x` and `y`, so the crosswise name match never engaged and the file was
// declined by the short-name rule instead of by the guard it claimed to pin. Deleting
// `DelegatesToItsOwnName` left it green. The callee below declares `left` and `right` so that the
// only thing standing between this call and a finding is the guard itself.
namespace Fixtures {
    sealed class Ascending {
        public int Compare(string left, string right) => string.CompareOrdinal(left, right);
    }

    sealed class Descending {
        readonly Ascending inner = new Ascending();

        public int Compare(string left, string right) => inner.Compare(right, left);
    }
}
