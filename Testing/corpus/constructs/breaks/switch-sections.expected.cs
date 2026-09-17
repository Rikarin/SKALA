// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
// A `switch` statement written on one line (issue #374). Nothing in the corpus wrote one, so the
// sweep never saw that Skala left it as written while the oracle puts each section on a line of
// its own — the same rule that already expanded the one-line `if` block beside it. What stays
// joined *inside* a section is measured here too: one simple statement, with or without its
// `break;`, shares the label's line and fills when it overflows; anything else breaks one
// statement per line, the first off the label; stacked labels each take a line; and a `switch`
// that is somebody's embedded statement leaves the header's line.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class SwitchSections {
    int field;

    void IfBlockBeside(bool b) {
        if (b) {
            M();
            M();
        }
    }

    void TwoSections(object o) {
        switch (o) {
            case 1: break;
            case 2: break;
        }
    }

    void OneStatementAndBreak(object o) {
        switch (o) {
            case 1: M(); break;
        }
    }

    void TwoStatementsAndBreak(object o) {
        switch (o) {
            case 1:
                M();
                M();
                break;
        }
    }

    void ThreeStatements(object o) {
        switch (o) {
            case 1:
                M();
                M();
                M();
                break;
        }
    }

    void BracedSection(object o) {
        switch (o) {
            case 1: {
                M();
                break;
            }
        }
    }

    void DefaultSection(object o) {
        switch (o) {
            case 1: break;
            default: break;
        }
    }

    void StackedLabels(object o) {
        switch (o) {
            case 1:
            case 2: break;
            case 3:
            case 4: M(); break;
        }
    }

    void NestedIf(object o, bool b) {
        switch (o) {
            case 1:
                if (b) M();
                break;
        }
    }

    void NestedIfBlock(object o, bool b) {
        switch (o) {
            case 1:
                if (b) {
                    M();
                }

                break;
        }
    }

    int Returns(object o) {
        switch (o) {
            case 1: return 1;
            default: return 0;
        }
    }

    void Throws(object o) {
        switch (o) {
            case 1: M(); break;
            default: throw new InvalidOperationException();
        }
    }

    void Assignment(object o) {
        switch (o) {
            case 1: field = 1; break;
            case 2: field++; break;
        }
    }

    void TwoAssignments(object o) {
        switch (o) {
            case 1:
                field = 1;
                field = 2;
                break;
        }
    }

    void Declaration(object o) {
        switch (o) {
            case 1:
                var y = 1;
                field = y;
                break;
        }
    }

    void DeclarationAndBreak(object o) {
        switch (o) {
            case 1:
                var y = 1;
                break;
        }
    }

    void StatementThenReturn(object o) {
        switch (o) {
            case 1:
                M();
                return;
        }
    }

    void StatementThenGoto(object o) {
        switch (o) {
            case 1:
                M();
                goto case 2;
            case 2: goto default;
            default: break;
        }
    }

    void Continue(object o) {
        while (field > 0) {
            switch (o) {
                case 1: continue;
                case 2:
                    M();
                    continue;
            }
        }
    }

    IEnumerable<int> Yields(object o) {
        switch (o) {
            case 1: yield return 1; break;
            case 2: yield break;
            case 3:
                yield return 3;
                yield break;
        }
    }

    void Lambda(object o) {
        switch (o) {
            case 1: Run(() => { M(); }); break;
        }
    }

    async Task Awaits(object o) {
        switch (o) {
            case 1: await Task.Delay(1); break;
        }
    }

    void Patterns(object o) {
        switch (o) {
            case 1: M(); break;
            case int i when i > 2: M(); break;
            case string { Length: 3 }: break;
        }
    }

    void EmbeddedOwner(object o) {
        switch (o) {
            case 1:
                lock (o) M();
                break;
            case 2:
                while (field > 0) M();
                break;
        }
    }

    void Empty(object o) {
        switch (o) { }
    }

    void FollowedByAStatement(object o) {
        switch (o) {
            case 1: break;
            case 2: break;
        }

        M();
    }

    void SectionsOnTheBraceLine(object o) {
        switch (o) {
            case 1: break;
            case 2: break;
        }
    }

    void SectionsJoinedInsideBrokenBraces(object o) {
        switch (o) {
            case 1: break;
            case 2: break;
        }
    }

    void KeptLabelBreak(object o) {
        switch (o) {
            case 1:
                M(); break;
            case 2:
                M();
                break;
            case 3:
                M();
                break;
        }
    }

    void Fits(object o) {
        switch (o) {
            case 1: MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM(); break;
        }
    }

    void OneOver(object o) {
        switch (o) {
            case 1:
                MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM(); break;
        }
    }

    void FarOver(object o) {
        switch (o) {
            case 1:
                MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM();
                break;
        }
    }

    void KeptLabelBreakTailFits(object o) {
        switch (o) {
            case 1:
                MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM(); break;
        }
    }

    void KeptLabelBreakTailOneOver(object o) {
        switch (o) {
            case 1:
                MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM();
                break;
        }
    }

    int SwitchExpressionBeside(object o) =>
        o switch {
            1 => 1,
            2 => 2,
            _ => 0
        };

    void EmbeddedSwitch(object o, bool b) {
        if (b)
            switch (o) {
                case 1: break;
                case 2: break;
            }
    }

    void EmbeddedEmptySwitch(object o, bool b) {
        while (b)
            switch (o) { }
    }

    void EmbeddedTry(bool b) {
        if (b)
            try {
                M();
            } finally {
                M();
            }
    }

    void Nested(object o) {
        switch (o) {
            case 1:
                switch (field) {
                    case 1: break;
                }

                break;
        }
    }

    void M() { }

    void Run(Action action) { }

    void MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM() { }

    void MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM() { }

    void MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM() { }

    void MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM() { }

    void MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM() { }
}
