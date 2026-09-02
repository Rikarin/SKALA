// ⚠ The wall. A settable auto-property is written by reflection with nothing in the source
// saying so — Newtonsoft.Json writes a private setter by default — so folding it into a
// computed property changes what deserialization produces.
public sealed class Settable {
    public string Scheme { get; set; } = "https";
}
