// analyzer-option: skala_parentheses_redundancy_style = Remove
// ⚠ The formatter parses option values case-sensitively and refuses `Remove`, so the default stays in
// force and the arranger keeps these parentheses. Silencing the rule on it would leave the shape
// unreported and unrepaired.
class C {
    int M(int a, int b, int c) => a | b & c;
}
