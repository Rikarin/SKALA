public static class Spread {
    static int Count(string name, params int[] values) => name.Length + values.Length;

    public static int One(string name) => Count(name, 0);
}
