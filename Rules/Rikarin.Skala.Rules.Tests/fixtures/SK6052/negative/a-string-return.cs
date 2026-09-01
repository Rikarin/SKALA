namespace Contoso.Design;

// `string` is an `IEnumerable<char>` and is never a sequence in the sense this rule means. It is
// excluded by matching the declared type rather than by asking what the type is assignable to — which
// is the same decision that keeps every concrete collection `[]` cannot construct out of the rule.
public sealed class Labels {
    public string For(int id) {
        return null;
    }
}
