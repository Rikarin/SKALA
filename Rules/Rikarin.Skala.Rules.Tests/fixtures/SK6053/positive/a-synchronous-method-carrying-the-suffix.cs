namespace Contoso.Design;

// The other direction, and it matters as much: the name tells every caller to write an `await` that
// will not compile, or to assume work is happening in the background that is not.
public sealed class Store {
    public int LoadAsync(int id) => id;
}
