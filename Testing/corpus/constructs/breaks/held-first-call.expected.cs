// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
namespace Constructs.Breaks;

// Issue #528 (SK-DIV-0331). A chain's held first call breaks too when it does not fit on the receiver's
// line and its line below ends well short of the margin; past that the oracle holds it and chops its
// arguments. A `!` before the call ends the receiver, and the call after it is the one held.
public class HeldFirstCall {
    void M() {
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(alpha)
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            .Select(alpha)
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            .Select(alpha)
            .Where(b);
        var y = sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            .Select(alpha)
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            .Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbb)
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            .Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbb)
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            .Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(
                aaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            )
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            .Select(bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
            .Where(b);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(
                bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            )
            .Where(b);
        var x = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValuexxxxx)!
            .Where(beta)
            .ToList();
        var y6 = sourceWithAVeryLongNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
            .Select(alpha)
            .Where(b);
    }
}
