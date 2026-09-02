// The lifted `!` is three-valued. The rewrite is arguably sound and is refused anyway: the rule
// asks one question about the operand's type and does not carry an exception to it.
class C {
    public static bool? Run(bool? maybe) => !!maybe;
}
