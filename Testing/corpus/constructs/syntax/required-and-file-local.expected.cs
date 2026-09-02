// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
using System.Collections.Generic;

// `required` members occurred eight times in three files, a `file`-local type once and a static
// abstract interface member once. None is a syntax kind — all three are modifier tokens — so the kind
// census reports full coverage of the declarations that carry them. Each widens a member's modifier
// list, which is what decides whether the declaration still fits on one line.
file class FileLocal {
    public required string Name { get; init; }

    public required IReadOnlyList<string> Aliases { get; init; }

    public required int Count { get; set; }
}

file readonly struct FileLocalStruct {
    public required int Value { get; init; }
}

file static class FileLocalStatic {
    public static int Zero => 0;
}

class RequiredMembers {
    public required string Name;

    public required string OverflowingPropertyNameThatPushesTheAccessorHolderPastTheMargin { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Index { get; init; }

    protected internal required int ManyModifiers { get; set; }
}

interface INumericLike<TSelf> where TSelf : INumericLike<TSelf> {
    static abstract TSelf Zero { get; }

    static abstract TSelf One { get; }

    static abstract TSelf operator +(TSelf left, TSelf right);

    static abstract TSelf Combine(TSelf left, TSelf right, TSelf carry, TSelf overflow, TSelf remainder, TSelf seed);

    static virtual TSelf Twice(TSelf subject) => TSelf.One + subject;
}

readonly struct Counter : INumericLike<Counter> {
    readonly int value;

    Counter(int value) => this.value = value;

    public static Counter Zero => new Counter(0);

    public static Counter One => new Counter(1);

    public static Counter operator +(Counter left, Counter right) => new Counter(left.value + right.value);

    public static Counter Combine(
        Counter left,
        Counter right,
        Counter carry,
        Counter overflow,
        Counter remainder,
        Counter seed
    ) =>
        left + right + carry + overflow + remainder + seed;
}
