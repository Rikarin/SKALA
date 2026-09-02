// ⚠ THE REFUTATION, pinned. Issue #30 and the brief that carried it both state that "`params` on
// an override is ignored entirely", implying a caller through the derived type loses expansion.
// That is not what the compiler does, and this was compiled rather than reasoned about:
//
//   * `params` DROPPED on an override — as here — still expands through the derived type.
//     `new Quiet().Accept("a", 1, 2)` compiles.
//   * `params` ADDED on an override whose base has none does NOT expand, through either
//     reference. Both call sites are CS1501.
//
// So an override's own `params` keyword is dead text in both directions: expansion is decided by
// the base, always. There is no behaviour to diverge and nothing for this rule to report. Roslyn
// says the same thing at the symbol level — `IParameterSymbol.IsParams` on the override below is
// `true` although no keyword is written — so the rule declines this by construction rather than
// by a filter, and deleting a guard could not make it fire.
//
// The interface direction is genuinely different and is the positive fixture.
namespace Fixtures {
    abstract class Loud {
        public virtual void Accept(string name, params int[] values) { }
    }

    sealed class Quiet : Loud {
        public override void Accept(string name, int[] values) { }
    }
}
