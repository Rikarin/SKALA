// The canary for #397's exemption: declining a partial property's definition must not decline an
// ordinary get-only auto-property in the same partial type. `Width` has no setter, no initializer
// and no constructor that assigns it; `Count` is the partial property, and is 42.
sealed partial class Window {
    public partial int Count { get; }

    public partial int Count => 42;

    public int Width { get; }

    public bool IsWide => Width > Count;
}
