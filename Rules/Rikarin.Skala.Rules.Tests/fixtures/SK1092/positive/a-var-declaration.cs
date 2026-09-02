public sealed class Pairs {
    public string Describe() {
        var pair = new System.Tuple<int, string>(1, "a");
        return pair.Item1 + pair.Item2;
    }
}
