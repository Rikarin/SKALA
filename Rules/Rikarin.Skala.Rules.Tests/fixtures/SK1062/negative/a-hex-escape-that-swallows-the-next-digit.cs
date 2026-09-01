// ⚠ `\x` is greedy up to four digits, so this is one escape denoting U+041B — not `\x41` followed
// by `B`. Reading it the obvious way and emitting "AB" would change the string.
public sealed class Greedy {
    public string Cyrillic() => "\x41B";
}
