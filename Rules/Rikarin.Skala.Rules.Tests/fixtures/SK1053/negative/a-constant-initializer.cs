// `var x = 5;` is dead code whose repair is deletion. `_ = 5;` is the same dead code with an
// assignment bolted on.
public sealed class Cache {
    public void Warm() {
        var size = 5;
    }
}
