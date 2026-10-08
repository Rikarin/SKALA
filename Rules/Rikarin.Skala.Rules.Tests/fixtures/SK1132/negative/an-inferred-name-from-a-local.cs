public sealed class Pairing {
    public int Use(int count, string name) {
        var pair = (count, name);
        return pair.Item1 + pair.Item2.Length;
    }
}
