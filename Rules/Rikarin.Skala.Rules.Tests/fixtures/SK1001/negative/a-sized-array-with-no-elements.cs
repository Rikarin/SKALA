// `new string[4]` is four nulls; `[]` is empty. There is nothing to convert.
public sealed class Names {
    public string[] All() {
        string[] names = new string[4];
        return names;
    }
}
