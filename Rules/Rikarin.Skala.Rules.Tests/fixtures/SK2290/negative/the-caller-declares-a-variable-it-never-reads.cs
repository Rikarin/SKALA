// ⚠ The deliberate fraction. This call site declares `length` and never reads it, which is `SK6040`'s
// finding at the call site — not this rule's at the declaration. Writing "declared but never read" a
// second time here would be the same analysis in two rules, and the two would disagree eventually.
// After `SK6040`'s fix the call reads `out _` and this rule becomes reachable on the same method.
class Reader {
    static bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }

    public bool Run(string line) => TryParseHeader(line, out var length);
}
