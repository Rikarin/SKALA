// A binary *pattern* chain the author broke at one link is chopped at every link, although the whole
// chain fits on its line (#483, SK-DIV-0124). In a switch arm, after `is`, in a `case` label and in an
// `if` condition alike, for `and` as for `or`, and broken at the inner link as at the outer.
//
// ⚠ A binary *expression* chain is not: `a && b` / `|| c` in the same arm comes back as written
// (SK-DIV-0109's rule), which is why the two `when` arms are here. And a break written *after* the
// combinator is not one of the chain's points (`wrap_before_binary_pattern_op = true`), so it joins.
class BinaryPatternChainKeptBreak {
    uint Arm(PixelFormat f) => f switch
    {
        PixelFormat.Rgba16Float or PixelFormat.Rg16Float
            or PixelFormat.Rgba16UNorm => 2u,
        _ => 0u
    };

    uint Inner(PixelFormat f) => f switch
    {
        PixelFormat.Rgba16Float
            or PixelFormat.Rg16Float or PixelFormat.Rgba16UNorm => 2u,
        _ => 0u
    };

    uint After(PixelFormat f) => f switch
    {
        PixelFormat.Rgba16Float or
            PixelFormat.Rg16Float or PixelFormat.Rgba16UNorm => 2u,
        _ => 0u
    };

    uint When(bool a, bool b, bool c) => a switch
    {
        _ when a && b
            || c => 2u,
        _ => 0u
    };

    void Statements(PixelFormat f) {
        switch (f) {
            case PixelFormat.Rgba16Float or PixelFormat.Rg16Float
                or PixelFormat.Rgba16UNorm:
                break;
        }

        var b = f is PixelFormat.Rgba16Float or PixelFormat.Rg16Float
            or PixelFormat.Rgba16UNorm;
        var c = f is PixelFormat.Rgba16Float and not PixelFormat.Rg16Float
            and not PixelFormat.Rgba16UNorm;
        var d = f is PixelFormat.Rgba16Float and PixelFormat.Rg16Float or PixelFormat.Bgra8
            or PixelFormat.Rgba16UNorm;
        var e = rune is >= 0x1100
            and <= 0x115F
            or >= 0x2E80
            and <= 0x303E
            or >= 0x3041
            and <= 0x33FF;
        if (f is PixelFormat.Rgba16Float or PixelFormat.Rg16Float
            or PixelFormat.Rgba16UNorm) { }
    }
}
