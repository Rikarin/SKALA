// analyzer-option: resharper_parentheses_redundancy_style = remove
// ⚠ The formatter reads `skala_parentheses_redundancy_style` under that one name; the ReSharper
// spelling is an unknown key to it (SK9001) and the default stays in force. The arranger keeps these
// parentheses, so the rule must not go quiet on a spelling the arranger never read.
class C {
    int M(int a, int b, int c) => a | b & c;
}
