using System.Collections.Generic;

public static class CollectionElement {
    // A collection initializer element writes no type of its own; the element type comes from the
    // collection, which is inference by another name and outside the whitelist.
    public static List<int?> Go(int value) => new List<int?> { new int?(value) };
}
