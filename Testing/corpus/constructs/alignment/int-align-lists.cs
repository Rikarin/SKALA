using System;

namespace Skala.Corpus.Alignment;

// The list-shaped members of the `int_align_*` family: `parameters`, `invocations` and
// `property_patterns`. Every key is false in the export, so this file is the unpadded shape and the
// option units are what pad it.
//
// ⚠ Each construct here is chopped by width under the repository's own configuration rather than by
// a second key. A run needs one construct per line, and a shape that only chops when some other
// option is flipped is a shape the per-option unit — which flips exactly one key — can never reach.
//
// ⚠ The family's two conditional-chain members live in int-align-ternary.cs beside this file, and
// not in it. `int_align_nested_ternary` and `int_align_binary_expressions` both pad a chain the
// oracle writes as `cond ? a :` with one member per line; Skala did not write that layout until the
// chain got a break plan of its own, which is why they were absent here for nine milestones.
public class IntAlignLists {
    // int_align_parameters: the parameter names pad out to a column past the widest type.
    public void ASignatureLongEnoughToChopUnderTheExportsOwnMargin(
        int first,
        string secondParameterName,
        double third,
        object aFourthParameterName
    ) {
    }

    public void Body(int flag, object other) {
        // int_align_invocations: adjacent single-line calls of the same method align every
        // argument. ⚠ The `Other` between the two groups ends the run rather than being skipped:
        // "invocations of the same method" is the key's own wording and the oracle honours it.
        Take(1, 2, 3);
        Take(10, 20, 30);
        Take(1000, 2000, 3000);
        Other(1, 2, 3);
        Take(100, 200, 300);

        // int_align_property_patterns: the `:` of each subpattern of a chopped property pattern.
        if (other is IntAlignLists { AShortOne: 111111111, SecondPropertyNameThatIsRatherLong: 2222222222, ThirdPropertyName: 33333333333, AFourthPropertyNameHere: 444444444 }) {
            Console.WriteLine(flag);
        }
    }

    public int AShortOne { get; set; }

    public int SecondPropertyNameThatIsRatherLong { get; set; }

    public int ThirdPropertyName { get; set; }

    public int AFourthPropertyNameHere { get; set; }

    static void Take(int alpha, int beta, int gamma) {
    }

    static void Other(int alpha, int beta, int gamma) {
    }
}
