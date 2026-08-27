// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
using System.Collections.Generic;

namespace Skala.Corpus.Arrangement;

// resharper_trailing_comma_in_multiline_lists = false and ..._in_singleline_lists = false, so an
// existing trailing comma is removed from both shapes. Which key applies is decided by whether the
// closing brace sits on a later line than the last element.
public class TrailingCommas {
    // Multiline, with a trailing comma to remove.
    public List<string> Multiline = new() {
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
    };

    // Multiline, already correct.
    public List<string> MultilineClean = new() {
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
        "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
    };

    // Single-line, with a trailing comma to remove.
    public int[] Singleline = [1, 2, 3,];

    // An object initializer is a list too.
    public Target Object = new() { First = 1, Second = 2, };

    // ⚠ An argument list is *not*: C# has no `f(a, b,)`, so this rule never touches one.
    public int Call() => Add(1, 2);

    static int Add(int a, int b) => a + b;

    public class Target {
        public int First { get; set; }
        public int Second { get; set; }
    }
}

// An enum member list admits a trailing comma and is governed by the same pair of keys.
public enum Trailing {
    Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
    Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
    Cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc,
}
