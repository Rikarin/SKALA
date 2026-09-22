using System;
using System.Threading;

namespace Constructs.Breaks;

// Issue #378. There was no break point before a `when` at all, so a case label whose property
// pattern ended at column 120 chopped the pattern to make room for the clause. The oracle breaks
// before the `when`, one level in from `case`, by the arm arrow's rule and not the `=`'s: the
// clause moves down exactly when `when` plus the condition's head up to its first break point has
// no room on the label's line, and stays — the arguments chop below it — when it has, even when the
// whole clause would have fitted on the line below. The lambda's arrow is a break point too, and it
// is what keeps `case { … } when static x =>` on the label's line: the `when` measures its head up
// to that point and stops. In an arm the `when` reads the same way; the arm's `=>` is never left
// past the margin (`=>` ending at 120 stays, 121 moves down with the body); a kept break before a
// `when` is kept. Seeds 13095184792041486380 and 11255509907099259375.
public sealed class CaseLabelWhen {
    public void PatternCloseAt120ThenWhen(object value) {
        switch (value) {
            case { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } when Bind(first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth):
                break;
            case { Length: > 0, Name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } when Bind(first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth):
                break;
            default:
                break;
        }
    }

    public void TypePatternWhenStays(object value) {
        switch (value) {
            case SomeVeryLongTypeName someVeryLongVariableName when Bind(first, second, third, fourth, fifth, sixth, seventh):
                break;
            case SomeVeryLongTypeName someVeryLongVariableName when Bind(firstArgumentValue, secondArgumentValue, thirdArgumentValue, fourthArgumentValue):
                break;
            default:
                break;
        }
    }

    public void WhenWithALambda(object value, CancellationToken cancellationToken) {
        switch (value) {
            case { Length: > 3, Name: "sssssssssssssssssssssss" } when static x => Convert<CancellationToken, CancellationToken>(x, cancellationToken, anotherArgument):
                break;
            case 1
                when x:
                break;
            default:
                break;
        }
    }

    public int ArmWithAWhen(object value) {
        var r = value switch {
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } when Bind(first, second, third, fourth, fifth, sixth, seventh) => Body(first),
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssss" } when Bind(first, second) => Body(firstArgumentValue, secondArgumentValue, thirdArgumentValue, fourthArgumentValue, fifthArgumentValue, sixthArgumentValue),
            SomeLongConstant.Value when xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx => Body(firstArgumentValue, secondArgumentValue, thirdArgumentValue, fourthArgumentValue, fifthArgumentValue, sixthArgumentValue),
            SomeLongConstant.Value when xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx => Body(firstArgumentValue, secondArgumentValue, thirdArgumentValue, fourthArgumentValue, fifthArgumentValue, sixthArgumentValue),
            1
                when x => 2,
            _ => 0
        };
        return r;
    }
}
