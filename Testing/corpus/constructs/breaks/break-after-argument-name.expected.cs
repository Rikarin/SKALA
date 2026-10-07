// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
namespace Constructs.Breaks;

// SK-DIV-0177 (issue #411). A named argument's `name:` is a break point, and the value lands on the
// argument's own column with no continuation level. The oracle takes it by the ordering rule's second
// question alone: the value moves down exactly when the line up to the value's first break point has
// no room — a string, an identifier, a binary operand or a call name past the margin — and stays
// beside its name when `Compute(`, `source.Select(`, `new Widget(` or `x => Compute(` fits. An
// attribute's `name:` is the same and an author's break after the colon is kept. Before this file
// no construct had a named argument that overflowed, and Skala had no point after the colon at all.
public class BreakAfterArgumentName {
    void UnbreakableA() {
        Outer(
            first: 1,
            name176:
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
        );
    }

    void UnbreakableB() {
        Outer(
            first: 1,
            name176: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
        );
    }

    void UnbreakableC() {
        Outer(
            first: 1,
            name176:
            "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss",
            last: 2
        );
    }

    void UnbreakableD() {
        Outer(
            first: 1,
            name176:
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
        );
    }

    void UnbreakableE() {
        {
            Outer(
                name176:
                "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            );
        }
    }

    void FirstpointA() {
        Outer(
            first: 1,
            name176:
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + beta
        );
    }

    void FirstpointB() {
        Outer(
            first: 1,
            name176: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            + beta
        );
    }

    void FirstpointC() {
        Outer(
            first: 1,
            name176: Compute(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                beta
            )
        );
    }

    void FirstpointD() {
        Outer(
            first: 1,
            name176: source.Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                .Where(beta)
        );
    }

    void FirstpointE() {
        Outer(
            first: 1,
            name176: new Widget(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                beta
            )
        );
    }

    void FirstpointF() {
        Outer(
            first: 1,
            name176: x => Compute(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                beta
            )
        );
    }

    void FirstpointG() {
        Outer(
            first: 1,
            name176:
            Computeaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa(
                alpha,
                beta
            )
        );
    }

    [Attr(
        first: 1,
        name176:
        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
    )]
    void OwnersA() { }

    void OwnersB() {
        var created = new Widget(
            first: 1,
            name176:
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
        );
    }

    void OwnersC() {
        Outer(
            first: 1,
            name176:
            Cast<Dictionary<string, Guid?>, AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA>(
                xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx,
                yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
            )
        );
    }

    void KeptA() {
        Outer(
            first: 1,
            name176:
            Compute(alpha, beta)
        );
    }

    void KeptB() {
        Outer(
            first: 1,
            name176:
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
        );
    }
}
