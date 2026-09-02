// The repair, and the shape `ParenthesesRedundancy.MayRemove` refuses to undo: an operand of a shift
// or bitwise operator keeps its parentheses at every value of every configuration key.
class C {
    int Shift(int value, int offset) => value << (offset + 1);

    int Sum(int a, int b) => (a + b) << 1;

    int Mask(int mask, int offset) => mask & (offset + 1);

    int Or(int a, int b, int c) => a | (b & c);
}
