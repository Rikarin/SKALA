// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
// The three `force_chop_compound_*` keys: a compound statement condition chopped at every operator
// of its root chain, however well it fits. Each key governs exactly one statement kind, so the
// `while` and `do` cases below do not move when only the `if` key is on.

class ForceChopCompound {
    // Three operands always chop, whatever the operands are.
    void IfThreeOperands(bool a, bool b, bool c) {
        if (a
            && b
            && c) {
            Use();
        }
    }

    // Two operands, neither a single token: chops.
    void IfTwoComplexOperands(Holder a, Holder b) {
        if (a.Flag
            && b.Flag) {
            Use();
        }
    }

    // Two operands, one of them a bare name: does not chop, at either value. The discriminator is
    // the token count and not the width — the identifiers here are far longer than `a.Flag`.
    void IfTwoOperandsOneSimple(bool anExceedinglyLongParameterName, Holder b) {
        if (anExceedinglyLongParameterName && b.Flag) {
            Use();
        }
    }

    // The root operator's own kind only: the `||` breaks and the `&&` does not.
    void IfMixedOperators(Holder a, Holder b, Holder c) {
        if (a.Flag && b.Flag
            || c.Flag) {
            Use();
        }
    }

    // A relational root is not a compound condition, and neither is a single `&`.
    void IfRelationalRoot(Holder a, Holder b) {
        if (a.Count > b.Count) {
            Use();
        }
    }

    void WhileThreeOperands(bool a, bool b, bool c) {
        while (a && b && c) {
            Use();
        }
    }

    void DoTwoComplexOperands(Holder a, Holder b) {
        do {
            Use();
        } while (a.Flag && b.Flag);
    }

    // Neither a `for` header nor a `return` is governed by any of the three keys.
    void ForHeader(bool a, bool b) {
        for (var i = 0; a && b; i++) {
            Use();
        }
    }

    bool ReturnCompound(Holder a, Holder b) {
        return a.Flag && b.Flag;
    }

    void Use() { }
}

class Holder {
    public bool Flag { get; set; }

    public int Count { get; set; }
}
