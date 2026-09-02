using System.Collections.Generic;

public sealed class Loading {
    static List<int> Build() => new();

    // The declared type is exactly the initializer's, so the `return` performs the same conversion
    // either way and deleting the declaration moves nothing.
    public static IEnumerable<int> All() {
        List<int> items = Build();
        return items;
    }
}
