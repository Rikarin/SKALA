namespace Contoso.Design;

// ⚠ The load-bearing decline. Every instance constructor is `private`, so nothing outside this
// declaration can derive from `Result` — the set of subclasses is fixed at compile time and the test
// over them cannot silently miss a case. This is a discriminated union emulated in C#, not a missing
// `virtual`, and the compiler is enforcing the exhaustiveness the rule exists to say nobody enforces.
public abstract class Result {
    Result() {
    }

    public string Describe() => this is Failure ? "failed" : "ok";

    public sealed class Success : Result;

    public sealed class Failure : Result;
}
