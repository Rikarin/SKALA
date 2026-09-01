using System.Diagnostics;

sealed class AttributedFixture {
    public void Use() => Trace("value");

    [Conditional("DEBUG")]
    void Trace(string message) { }
}
