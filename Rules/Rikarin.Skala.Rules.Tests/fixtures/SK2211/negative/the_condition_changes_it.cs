// ⚠ The false positive the corpus found, reduced. `while (digits-- != 0)` is Newtonsoft's
// `DateTimeUtils.CopyIntToCharArray`, and it terminates perfectly well: the *condition* writes
// `digits`, so the body has no reason to. `AnalyzeDataFlow` is asked about the body and answers
// correctly — the question was the incomplete half, and nothing was asking whether the condition
// was the thing doing the changing.
//
// The corpus was the only place this shape appeared. No fixture had it, both sabotage rounds on the
// variable walk stayed green over it, and the rule shipped its first draft reporting it.
class C {
    void CopyIntToCharArray(char[] chars, int start, int value, int digits) {
        while (digits-- != 0) {
            chars[start + digits] = (char)((value % 10) + 48);
            value /= 10;
        }
    }

    void PreIncrement(int count) {
        var i = 0;
        while (++i < count) {
            System.Console.WriteLine(i);
        }
    }

    void Assigned(int count) {
        var i = 0;
        while ((i = i + 2) < count) {
            System.Console.WriteLine(i);
        }
    }
}
