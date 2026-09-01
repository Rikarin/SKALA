public static class Boxed {
    // The receiver's type is read from the symbol: this call is `object.ToString`.
    public static string Describe(string name) => ((object)name).ToString()!;
}
