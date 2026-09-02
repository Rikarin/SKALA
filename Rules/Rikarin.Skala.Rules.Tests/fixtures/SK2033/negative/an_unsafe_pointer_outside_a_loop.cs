// ⚠ The `unsafe` spelling of `stackalloc`, which is the one this rule's territory is written in
// outside the `Span<T>` form — and which no fixture could carry at all until `allowUnsafe` was
// passed to the fixture compilation (#310): this file is CS0227 without it, so it fails loudly
// rather than quietly if the flag is ever tidied away. The allocation is outside every loop, so the
// rule must decline it.
class C {
    unsafe int M() {
        int* buffer = stackalloc int[64];
        var total = 0;
        for (var i = 0; i < 64; i++) {
            total += buffer[i];
        }

        return total;
    }
}
