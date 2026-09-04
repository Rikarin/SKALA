// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using System;

// An interpolated raw string occurred twice in the whole corpus and a UTF-8 literal once. Neither is
// a node kind of its own — both are ordinary string-literal nodes with a different token — so the kind
// census reports them as covered by StringLiteralExpression and InterpolatedStringExpression, and the
// gap is invisible to it. `resharper_csharp_indent_raw_literal_string` is the key that owns the
// closing-quote column, and it has no example with an interpolation hole in it.
class RawAndUtf8Strings {
    static ReadOnlySpan<byte> Utf8 => "alpha"u8;

    static ReadOnlySpan<byte> Utf8Wide =>
        "the alpha value is one and the bravo value is two and the charlie value is three and it runs long"u8;

    static readonly byte[] Utf8Concatenated = [.. "alpha"u8, .. "bravo"u8, .. "charlie"u8];

    static string SingleLine(string name) => $"""the name is {name} and nothing escapes it""";

    static string TwoDollars(string name) => $$"""the name is {{name}} and a literal {brace} survives""";

    static string MultiLine(string name, int count) =>
        $"""
         name:  {name}
         count: {count}
         """;

    static string MultiLineTwoDollars(string name, int count) =>
        $$"""
          {
            "name": "{{name}}",
            "count": {{count}}
          }
          """;

    static string Aligned(string name, int count) =>
        $"""
         {name,-20} {count,8:N0}
         {name,-20} {count,8:N0}
         """;

    static string Overflowing(string name, int count, bool flag) =>
        $"""the name is {name} and the count is {count} and the flag is {flag} and this line does not fit""";

    static string Plain() =>
        """
        no interpolation at all, for the pair that the indent key is decided against
        """;
}
