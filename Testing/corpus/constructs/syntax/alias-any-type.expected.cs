// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-31
// C# 12's alias-any-type: `using X = <any type>;` rather than `using X = <some name>;`. The corpus
// had aliases, but every one of them aliased a name, so the node the census counts
// (UsingDirective) was common while this construct was absent — another gap only a
// shape-aware probe can see. The right-hand side is a type rather than a name here, so
// `resharper_csharp_space_around_alias_eq` is asked about tuples, arrays and predefined types, and a
// long one asks where the alias may break.

using System;
using System.Collections.Generic;
using Age = int;
using Buffer = byte[];
using Grid = int[,];
using Jagged = string[][];
using Point = (int X, int Y);
using Unnamed = (int, int);
using Nested = (int X, (string Name, bool Flag) Inner);
using Handler = System.Action<string, int>;
using Index = System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>;
using Overflowing =
    (System.Collections.Generic.IReadOnlyList<string> Names, System.Collections.Generic.IReadOnlyDictionary<string, int>
    Counts, bool Flag);

class AliasAnyType {
    Age age = 1;
    Buffer buffer = new byte[16];
    Grid grid = new int[2, 2];
    Jagged jagged = [];
    Point point = (1, 2);
    Unnamed unnamed = (1, 2);
    Nested nested = (1, ("alpha", true));
    Handler handler = static (name, count) => { };
    Index? index;
    Overflowing overflowing = (new List<string>(), new Dictionary<string, int>(), false);

    static Point Combine(Point left, Point right) => (left.X + right.X, left.Y + right.Y);

    static Overflowing Widen(Index source, IReadOnlyList<string> names, IReadOnlyDictionary<string, int> counts) =>
        (names, counts, source.Count > 0);
}
