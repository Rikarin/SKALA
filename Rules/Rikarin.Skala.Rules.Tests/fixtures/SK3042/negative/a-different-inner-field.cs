public sealed class Registry {
    readonly object gate = new();

    string? name;

    string? label;

    public string Name() {
        // Two different fields. The shape rhymes with the idiom and is not it: nothing here reads
        // `name` outside the lock and then trusts the second read of the same field.
        if (name == null) {
            lock (gate) {
                if (label == null) {
                    label = "x";
                }

                name = label;
            }
        }

        return name;
    }
}
