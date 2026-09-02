// ⚠ The gap #306 found between two rules that each believed the other had this line. Shape A used
// to exclude *any* static member of the constructor's own type, on the ground that `SK2134`
// (`instance-write-to-static`) reports it — but `SK2134` binds the assignment target and gives up
// on `is not IFieldSymbol field`, so it never reports a property. `Current = this;` where `Current`
// is this type's own static *property* was excluded here and invisible there.
//
// Compare `negative/the-own-types-static-field.cs`, which is the same publication through a static
// field: that one is still excluded, because `SK2134` really does report it and two findings on
// one line in two vocabularies get acted on in neither.
public sealed class Session {
    public Session(string user) {
        Current = this;
        User = user;
    }

    public static Session? Current { get; private set; }

    public string User { get; }
}
