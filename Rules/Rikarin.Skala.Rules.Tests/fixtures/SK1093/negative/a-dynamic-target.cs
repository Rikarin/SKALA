public sealed class Dynamic {
    public object Get(object o) {
        var loose = (dynamic)o;
        return loose;
    }
}
