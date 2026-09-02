// ⚠ The `?:` row this rule was drafted with is gone, because the grammar makes it unreachable: a
// conditional expression binds looser than every binary operator, so `value << flag ? 1 : 2` parses
// as `(value << flag) ? …` and the `?:` is never an operand. Written the only two ways it can be,
// both correct and both silent.
class C {
    int Parenthesised(int value, bool flag) => value << (flag ? 1 : 2);

    int Chained(bool a, bool b) => a ? 1 : b ? 2 : 3;
}
