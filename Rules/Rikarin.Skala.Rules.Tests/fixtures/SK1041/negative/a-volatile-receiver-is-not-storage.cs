// ⚠ #423: two reads of a `volatile` field are two ordered reads another thread may come between, so
// a `volatile` link in the receiver is not storage.
public sealed class Box {
    public int X;
}

public sealed class Holder {
    volatile Box current = new();

    public void Bump() {
        current.X = current.X + 1;
    }
}
