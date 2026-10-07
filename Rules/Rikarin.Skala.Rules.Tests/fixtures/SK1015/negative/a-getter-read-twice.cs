// ⚠ The test and the cast read `Item` twice; `Item is string text` reads it once. Measured for
// #412's audit with a getter that answers differently the second time: "second" before, "first"
// after. A property with a body is not storage.
public sealed class Owner {
    int reads;

    public object Item {
        get {
            reads++;
            return reads == 1 ? "first" : "second";
        }
    }

    public string Describe() {
        if (Item is string) {
            var text = (string)Item;
            return text;
        }

        return "";
    }
}
