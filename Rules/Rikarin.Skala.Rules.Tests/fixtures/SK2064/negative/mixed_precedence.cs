// ⚠ `&` binds tighter than `|`, and both bind tighter than `&&` and `||`. Swapping one token in
// `a | b & c` would turn `a | (b & c)` into `(a | b) && c` — a different program, from a fix the
// catalogue calls safe. The rule declines the whole expression rather than risk it.
class C {
    bool M(bool a, bool b, bool c) => a | b & c;

    bool N(bool a, bool b, bool c) => a & b | c;

    bool P(bool a, bool b, bool c) => a & b ^ c;
}
