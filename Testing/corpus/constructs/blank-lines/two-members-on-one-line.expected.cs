// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    // ⚠ Every member gets a line of its own, and `csharp_preserve_single_line_blocks = true` in the
    // export does not stop it — ReSharper ignores that key. The shape is here for the idempotency
    // property as much as for the fidelity number: a member that shares a line has no stable notion
    // of "single line", which is what the blank-line keys branch on, so the first pass and the
    // second disagreed about the blank line between the members until M3 split them.
    public int A => 1;
    public int B => 2;
    public int C => 3;
}
