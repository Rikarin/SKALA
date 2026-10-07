// A labelled statement written on its label's line (#433). The oracle puts every statement but an empty
// one on a line of its own at the label's indent — an expression, a declaration, a loop, an if, a block
// and a second label — breaks after a block comment that follows the colon, and keeps an empty
// statement behind its label as `label: ;`. The gap in front of the colon is the author's: `a :` stays,
// `a:` stays, a run collapses. `goto-and-labels.cs` writes every label on its own line already, which is
// why the corpus never showed any of it. Skala joined them all and closed every gap before the colon.
public class LabelledStatementOnTheLabelsLine {
    void M(int a) { }

    void Labels(int k) {
        a1: M(1);
        a2 :M(2);
        a3  :  M(3);
        a4:M(4);
        b1: { M(6); }
        b3: /*c1*/ M(8);
        b4: // c2
        M(9);
        b5: ;
        e1:;
        b6: b7: M(10);
        b8: var v = 1;
        b9: for (var i = 0; i < 1; i++) { }
        switch (k) {
            case 1:
                c1: M(11);
                break;
        }

        f1 /*c*/: M(12);
        f2/*c*/ : M(13);
        f5: { }
        goto a1;
    }
}
