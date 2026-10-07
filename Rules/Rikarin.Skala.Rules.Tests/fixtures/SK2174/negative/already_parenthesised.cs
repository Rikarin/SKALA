// The repair, and the shape `ParenthesesRedundancy.MayRemove` refuses to undo at the export's
// `remove_if_not_clarifies_precedence`: a binary operand of a shift or bitwise operator keeps its
// parentheses. ⚠ This said "at every value of every configuration key", and at `remove` the arranger
// strips them (#394) — which is why the rule is silent there.
class C {
    int Shift(int value, int offset) => value << (offset + 1);

    int Sum(int a, int b) => (a + b) << 1;

    int Mask(int mask, int offset) => mask & (offset + 1);

    int Or(int a, int b, int c) => a | (b & c);
}
