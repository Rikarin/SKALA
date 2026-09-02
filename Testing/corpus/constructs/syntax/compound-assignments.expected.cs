// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
using System;

// ModuloAssignmentExpression (`%=`) and UnsignedRightShiftAssignmentExpression (`>>>=`) occurred
// nowhere; RightShiftAssignmentExpression occurred once and LeftShiftAssignmentExpression twice. All
// of them are Continuation nodes, which means SK-DIV-0005's question — where the right-hand side goes
// when the line does not fit — is asked of each of them, and `resharper_csharp_space_around_assignment_op`
// is asked of every one. The right-hand sides below are sized so that both questions have to be answered.
class CompoundAssignments {
    static int Every(int alpha, int bravo) {
        alpha += bravo;
        alpha -= bravo;
        alpha *= bravo;
        alpha /= bravo;
        alpha %= bravo;
        alpha &= bravo;
        alpha |= bravo;
        alpha ^= bravo;
        alpha <<= bravo;
        alpha >>= bravo;
        alpha >>>= bravo;
        return alpha;
    }

    static void Overflowing(int accumulator, int alpha, int bravo, int charlie, int delta, int echo, int foxtrot) {
        accumulator %= alpha + bravo + charlie + delta + echo + foxtrot + alpha + bravo + charlie + delta + echo;
        accumulator >>>= alpha + bravo + charlie + delta + echo + foxtrot + alpha + bravo + charlie + delta + echo;
        accumulator <<= alpha + bravo + charlie + delta + echo + foxtrot + alpha + bravo + charlie + delta + echo;
    }

    static void Invoked(int accumulator, Func<int, int, int> combine, int alpha, int bravo) {
        accumulator %= combine(alpha, bravo) + combine(bravo, alpha) + combine(alpha, alpha) + combine(bravo, bravo);
        accumulator >>>= combine(alpha, bravo) & combine(bravo, alpha) & combine(alpha, alpha) & combine(bravo, bravo);
    }

    static void Chained(ref int accumulator, int[] subjects, int index) {
        subjects[index] %= accumulator;
        subjects[index] >>>= accumulator;
    }
}
