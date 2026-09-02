using System;

// ⚠ The fixture that isolates the "declared by `System.MemoryExtensions`" guard. Everything else here
// is a positive: the receiver is a character span and the argument is a constant string. Only the
// declaring type differs — and a method somebody else wrote has no defined relationship to `is`.
//
// `a-linq-sequence-equal` does not reach this guard: its receiver is not a span, so the receiver-type
// guard declines it first. Deleting the declaring-type check turned nothing red until this file
// existed.
public static class Custom {
    public static bool SequenceEqual(this ReadOnlySpan<char> span, string other) => other.Length == 0;
}

public static class Names {
    public static bool IsWorld(ReadOnlySpan<char> name) => name.SequenceEqual("world");
}
