using System;

namespace Constructs.Breaks;

// Issue #378. A property pattern heading a switch expression arm is chopped by its OWN extent — up
// to its closing brace and whatever follows it up to the next movable break — never to rescue the
// arm's body: `}` at 120 stays and 121 chops. Before this file the pattern measured its
// rest-of-line straight through the arrow into the body and chopped itself at `}` = 119 on pass
// one; on pass two the body's fill had left a kept break that ended the measure, and the pattern
// joined again (the Nightly fuzzer's seed 2801382500469017888). The arm's arrow is a break point of
// its own now: the body leaves the head's line exactly when the head up to the body's first break
// point does not fit — `1 => Body(` stays and the arguments chop, whatever would have fit on the
// line below — and the arrow itself is never left past the margin: `}` at 117 breaks after the
// arrow, `}` at 118 breaks before it and the body follows the arrow on its line. A pattern that
// overflows puts its braces apart first and chops the subpatterns only when they still do not fit
// at the continuation column, in an arm, in a case label and under `is` alike; a body with no break
// point of its own is part of the head's line, so `{ … } => 2u,` at 122 chops the pattern and not
// the arrow.
public sealed class PropertyPatternHead {
    public int ArmCloseAt120(object value) {
        var r = value switch {
            { Length: > 0, Name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } => Body(first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth),
            _ => 0
        };
        return r;
    }

    public int ArmCloseAt117(object value) {
        var r = value switch {
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } => Body(first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth),
            _ => 0
        };
        return r;
    }

    public int ArmCloseAt118(object value) {
        var r = value switch {
            { Length: > 0, Name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } => Body(first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth),
            _ => 0
        };
        return r;
    }

    public int ArmCloseAt121(object value) {
        var r = value switch {
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } => Body(first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth),
            _ => 0
        };
        return r;
    }

    public int ArmWithATypeAt121(object value) {
        var r = value switch {
            SomeType { Length: > 0, Name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } => 1,
            _ => 0
        };
        return r;
    }

    public int ArmSevenSubpatterns(object value) {
        var r = value switch {
            SomeType { Length: > 3, Name: "ssssssssssssssssssssssssssssss", Kind: not null, Value.Length: > 2, Other: "ssssssssssssssssssssssssssssssssss", Last: 1, Tail: "ssssssssssssssssss" } => 1,
            _ => 0
        };
        return r;
    }

    public uint ArmShortBodyAt122(object value) {
        var r = value switch {
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } => 2u,
            _ => 0u
        };
        return r;
    }

    public int ArmArgumentsChopBeforeTheArrow(object value) {
        var r = value switch {
            1 => Body(firstArgumentValue, secondArgumentValue, thirdArgumentValue, fourthArgumentValue, fifthArgumentValue, sixthArgumentValue),
            SomeLongConstantName.SomeMemberValue.AnotherMemberName.YetAnother => Body(first, second, third, fourth, fifth),
            _ => 0
        };
        return r;
    }

    public int ArmBodyWithNoBreakPoint(object value) {
        var r = value switch {
            SomeLongConstantName.SomeMemberValue.AnotherMemberName.YetAnotherOne.AndMore => SomeVeryLongIdentifierWithNoBreakPointsInsideItAtAll,
            _ => 0
        };
        return r;
    }

    public int ArmKeptBreaksStay(object value) {
        var r = value switch {
            1 =>
                Body(first),
            2
                => Body(second),
            _ => 0
        };
        return r;
    }

    public void CaseLabelColonAt119And121(object value) {
        switch (value) {
            case { Length: > 0, Name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" }:
                break;
            case { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" }:
                break;
            case { Length: > 3, Name: "ssssssssssssssssssssssssssssss", Kind: not null, Value.Length: > 2, Other: "ssssssssssssssssssssssssssssssssss", Last: 1, Tail: "ssssssssssssssssss" }:
                break;
            default:
                break;
        }
    }

    public bool IsPatternAt121(object value) {
        var b = value is { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" };
        return b;
    }
}
