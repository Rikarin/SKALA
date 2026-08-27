// ⚠ "Measured per declaration, not per symbol, because that is the file a person opens" — rules.json,
// SK7004. A partial type whose other half is large is not this file's problem, and a fixture is
// compiled on its own, so this declaration is measured on its own.
public sealed partial class PartialTypeMeasuredPerFile {
    public int A() => 1;

    public int B() => 2;

    public int C() => 3;
}
