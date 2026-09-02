// ⚠ In a struct the backing field is the size. Removing it changes `sizeof`, marshalling and
// every blittable assumption made about the layout.
public struct Box {
    public Box() { }

    public int Maximum { get; } = 1;
}
