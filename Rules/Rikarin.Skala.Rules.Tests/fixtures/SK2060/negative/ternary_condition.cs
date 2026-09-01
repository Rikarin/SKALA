// ⚠ Assignment binds looser than `?:`, so this parses as `x = (flag = (other ? true : false))` —
// the assignment is never the ternary's condition. The parenthesised spelling below is the only way
// to write one, and it is the deliberate form the rule exempts anyway.
class C {
    int M(bool flag, bool other) {
        var x = flag = other ? true : false;
        var y = (flag = other) ? 1 : 2;
        return x ? y : 0;
    }
}
