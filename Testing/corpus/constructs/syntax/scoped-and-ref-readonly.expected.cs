// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using System;

// `scoped` (C# 11) and `ref readonly` parameters (C# 12) occurred nowhere. Neither is a syntax kind
// of its own — `scoped` on a parameter is a modifier token and only `scoped ref` inside a local
// declaration produces the ScopedType node — so the kind census could not see either gap. Both widen
// a parameter list, which is where `wrap_parameters_style` and
// `resharper_csharp_indent_method_decl_pars` are decided.
ref struct ScopedAndRefReadonly {
    Span<int> window;

    static void One(scoped Span<int> window) { }

    static void Ref(scoped ref int cursor) { }

    static void ReadOnly(ref readonly int origin) { }

    static void In(in int origin) { }

    static void Mixed(
        scoped ReadOnlySpan<char> text,
        ref readonly int origin,
        scoped ref int cursor,
        out int written
    ) =>
        written = 0;

    static int Overflowing(
        scoped ReadOnlySpan<char> text,
        scoped ref int cursor,
        ref readonly int origin,
        int alpha,
        int bravo
    ) =>
        cursor + origin + alpha + bravo + text.Length;

    // ScopedType: `scoped` in front of a local's type rather than a parameter's.
    static void Locals(Span<int> subject) {
        scoped Span<int> window = subject;
        scoped ref int cursor = ref window[0];
        scoped ref readonly int origin = ref window[0];
    }

    // A `scoped` receiver in a lambda's parameter list, where the modifier has to survive the
    // lambda-parens arrangement keys as well.
    delegate int Reader(scoped ReadOnlySpan<char> text);

    static readonly Reader Read = static (scoped ReadOnlySpan<char> text) => text.Length;
}
