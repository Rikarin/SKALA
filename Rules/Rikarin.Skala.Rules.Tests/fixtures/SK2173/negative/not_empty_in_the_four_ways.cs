// ⚠ The pattern must be empty in all four ways. A subpattern, a type, a positional clause and a
// designation each make it something other than a null check.
class C {
    bool WithSubpattern(string? text) => text is not { Length: 0 };

    bool WithType(object? value) => value is not string { };

    bool Positional(Pair? pair) => pair is not (1, 2);

    bool WithDesignation(object? value) => value is not { } bound;
}

sealed class Pair {
    public void Deconstruct(out int left, out int right) {
        left = 1;
        right = 2;
    }
}
