// analyzer-option: skala_parentheses_redundancy_style = remove
// ⚠ #394. At `remove` — ReSharper's "Always" — `skala arrange` strips the parentheses this rule's fix
// would add, as the oracle does, so reporting here made `skala fix` and `skala arrange` undo each
// other on every run. The repository has said it does not want these parentheses.
class C {
    int M(int a, int b, int c) => a | b & c;

    int Shift(int value, int offset) => value << offset + 1;

    int Mask(int mask, int offset) => mask & offset + 1;
}
