// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
using System;

namespace Skala.Corpus.Alignment;

// The two conditional-chain members of the `int_align_*` family: `nested_ternary` and
// `binary_expressions`. Both keys are false in the export, so this file is the unpadded shape and
// the option units are what pad it.
//
// ⚠ Both pad the same rows and pad them to different slots, which is measured one key at a time
// against `jb cleanupcode` 2025.2.6:
//
//   int_align_nested_ternary      var t = flag > 10 ? "a" :        int_align_binary_expressions
//                                     flag > 5  ? "bb" :               flag     > 5 ? "bb" :
//                                     flag > 1  ? "ccc" : "d";         flag     > 1 ? "ccc" : "d";
//
// ⚠ `int_align_binary_expressions` is narrower than its name and wider than the chain. With the key
// on, the oracle moves nothing in adjacent assignments with binary right-hand sides, in a binary
// chain chopped one operand per line, in adjacent `if` conditions, or in binary expressions used as
// arguments, as initializer elements or as switch-expression arm results. It does move two shapes:
// the conditional chain, and adjacent local variable *declarations* whose initializers are binary.
// Only the first is implemented, which is why `int_align_nested_ternary` is Tier A on this file and
// `int_align_binary_expressions` is not — see Declarations below and PhaseOneOptions.
//
// ⚠ The chain here is chopped by width under the repository's own configuration rather than by a
// second key: a run needs one member per line, and a shape that only chops when some other option
// is flipped is a shape the per-option unit — which flips exactly one key — can never reach. The
// conditions are of three different widths so that a column is actually paid for.
public class IntAlignTernary {
    public string Chain(int flag) {
        return flag > 100000 ? "the first branch here" :
            flag > 500 ? "the second branch here" :
            flag > 1 ? "third" : "d";
    }

    public string Widths(int flag, int other) {
        return flag > 1 ? "the first branch here" :
            other > 50000 ? "the second branch here" :
            flag > 500 ? "third" : "d";
    }

    // ⚠ A member joins the run while its `?` is on its condition's own line, which this layout also
    // satisfies — the `: flag > 500 ?` lines carry both. Measured: the oracle pads this one too, at
    // both keys, and it is here so that "the run is the chain" is read as a statement about the
    // chain rather than about the trailing-colon layout.
    public string AtTheSigns(int flag) {
        return flag > 100000 ? "the first branch here"
            : flag > 500 ? "the second branch here"
            : flag > 1 ? "third" : "d";
    }

    // ⚠ The second shape `int_align_binary_expressions` moves, and the reason that key is not Tier A
    // on this file. `int_align_nested_ternary` leaves these two alone; `int_align_binary_expressions`
    // pads both the `>` and the `&&` into a column, and Skala does not. Kept here rather than
    // removed, because a fixture trimmed to what the implementation already does is a measurement
    // managing its own result.
    public void Declarations(int flag, int other) {
        var first = flag > 1 && other > 2;
        var secondName = flag > 100000 && other > 2;
        Console.WriteLine(first);
        Console.WriteLine(secondName);
    }
}
