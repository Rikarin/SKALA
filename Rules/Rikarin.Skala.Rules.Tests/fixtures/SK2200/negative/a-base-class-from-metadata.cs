// #431: the base constructor is compiled code the rule cannot read, and a framework base constructor
// is free to call a virtual member this type overrides. Declined without looking further.
public sealed class LookupError : System.Exception {
    readonly int code = 5;

    public LookupError(int given) {
        code = given;
    }

    public int Code => code;
}
