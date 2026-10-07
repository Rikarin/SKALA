// analyzer-option: skala_parentheses_redundancy_style = remove_if_not_clarifies_precedence
// The export's value, written out: the arranger keeps a binary operand of a bitwise operator here,
// so the parentheses the fix adds stay.
class C {
    int M(int a, int b, int c) => a | b & c;
}
