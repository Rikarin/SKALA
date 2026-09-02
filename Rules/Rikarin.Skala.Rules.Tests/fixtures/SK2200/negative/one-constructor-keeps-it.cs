// The initializer survives the second constructor, so it is the value that object has.
public sealed class Buffer {
    int size = 16;

    public Buffer(int given) {
        size = given;
    }

    public Buffer(bool _) {
    }

    public int Size => size;
}
