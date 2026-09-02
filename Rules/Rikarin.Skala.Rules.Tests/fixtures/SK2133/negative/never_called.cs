// ⚠ The exclusion that keeps this rule off the language feature. An unimplemented `partial void`
// that nothing calls is an extension point nobody has taken up: it costs nothing at runtime, and
// reporting it would put a finding on the feature rather than on a mistake.
partial class Importer {
    partial void OnCreated();

    public void Create() {
    }
}
