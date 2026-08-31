using System;

// GotoDefaultStatement occurred nowhere; GotoCaseStatement occurred once, GotoStatement twice and
// LabeledStatement twice. A label is an Embedded node with an outdent key of its own
// (`resharper_csharp_outdent_statement_labels`), and the goto-into-a-switch-section forms are the
// only statements whose target is a switch label rather than an identifier.
class GotoAndLabels {
    static int Switched(int subject, int alpha) {
        switch (subject) {
            case 0:
                goto case 1;
            case 1:
                alpha++;
                goto default;
            case 2:
                goto case 0;
            default:
                return alpha;
        }
    }

    static int Labelled(int subject) {
        var count = 0;

        start:
        count++;
        if (count < subject) {
            goto start;
        }

        nested:
        {
            if (count < subject * 2) {
                count++;
                goto nested;
            }
        }

        return count;
    }

    static int Nested(int[] subjects, int wanted) {
        for (var outer = 0; outer < subjects.Length; outer++) {
            for (var inner = 0; inner < subjects.Length; inner++) {
                if (subjects[outer] + subjects[inner] == wanted) {
                    goto found;
                }
            }
        }

        return -1;

        found:
        return wanted;
    }
}
