// ⚠ The trap this fixture exists to pin. A positional record's property is *not* implicitly
// declared in the sense a test on `IsImplicitlyDeclared` would expect — the parameter is where it is
// written down, and both symbols point at the same `ParameterSyntax`. A rule that filtered on that
// flag would be dead everywhere. This rule reads a property *declaration* and a positional property
// has none, so it is out by construction rather than by a filter, and it is `{ get; init; }` besides.
record Point(int X, int Y) {
    public int Sum => X + Y;
}
