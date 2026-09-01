public sealed class Boxes {
    public object[] All(string first, object extra) => [.. new string[] { first }, extra];
}
