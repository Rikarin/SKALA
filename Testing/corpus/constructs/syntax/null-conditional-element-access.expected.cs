// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-31
using System;
using System.Collections.Generic;

// ElementBindingExpression — the `?[…]` half of a null-conditional access — occurred nowhere, while
// MemberBindingExpression is common. It is the bracketed member of the conditional-access family, so
// it is where `resharper_csharp_space_before_array_access_brackets` meets
// `resharper_slate_wrap_chained_member_access`, and a chain has to be long enough to chop for the
// second of those to have anything to say.
class NullConditionalElementAccess {
    static string? One(IReadOnlyList<string>? subjects) => subjects?[0];

    static string? Chained(IReadOnlyDictionary<string, IReadOnlyList<string>>? index, string key) =>
        index?[key]?[0]?.Trim();

    static int? Mixed(Dictionary<string, string[]>? index, string key) => index?[key]?.Length;

    static string? Overflowing(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>>? index, string key) =>
        index?[key]?[0]?[0]?.Trim()?.ToUpperInvariant()?.Substring(0, 3)?.PadLeft(8, '-')?.TrimEnd('-');

    static void Assigned(IList<int>? subjects) {
        var read = subjects?[0];
        var counted = subjects?[subjects.Count - 1];
        var invoked = subjects?[0].ToString();
    }

    // A conditional access whose whenNotNull is an invocation of an indexed member: the binding and
    // the argument list nest, and both carry wrapping keys.
    static string? Invoked(Func<int, IReadOnlyList<string>>? factory) => factory?.Invoke(0)?[0]?.ToString()?.Trim();
}
