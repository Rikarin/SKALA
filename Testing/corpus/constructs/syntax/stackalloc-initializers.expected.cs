// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-31
using System;

// ImplicitStackAllocArrayCreationExpression — `stackalloc[] { … }` — occurred nowhere, while the
// explicitly typed StackAllocArrayCreationExpression occurred 28 times and never with an initializer
// long enough to wrap. The initializer is a BracedInitializer, so `wrap_array_initializer_style` and
// `resharper_csharp_use_continuous_indent_inside_initializer_braces` both apply to it, and neither
// has an example of the implicitly typed form.
class StackallocInitializers {
    static int Implicit() {
        Span<int> small = stackalloc[] { 1, 2, 3 };
        Span<int> wide = stackalloc[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584, 4181 };
        return small.Length + wide.Length;
    }

    static int Explicit() {
        Span<int> small = stackalloc int[] { 1, 2, 3 };
        Span<int> sized = stackalloc int[8];
        Span<int> wide = stackalloc int[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584 };
        return small.Length + sized.Length + wide.Length;
    }

    static int Nested(int alpha, int bravo) {
        ReadOnlySpan<int> computed =
            stackalloc[] { alpha + bravo, alpha - bravo, alpha * bravo, alpha / bravo, alpha % bravo };
        return computed[0];
    }

    static int Argument() => Consume(stackalloc[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597 });

    static int Consume(ReadOnlySpan<int> subjects) => subjects.Length;

    static unsafe int Pointer() {
        var buffer = stackalloc int[] { 1, 2, 3 };
        return buffer[0];
    }
}
