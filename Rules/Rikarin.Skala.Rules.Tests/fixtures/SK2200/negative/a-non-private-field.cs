// Anything outside the type can write it, so "every constructor overwrites it" is not the whole
// story about who sets this field.
public sealed class Page {
    public int Margin = 8;

    internal int Padding = 4;

    public Page(int given) {
        Margin = given;
        Padding = given;
    }
}
