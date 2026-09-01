class C {
    public static int Compare(string a, string b) => 0;
    public string ToLower() => "";
    bool M(C other) => Compare("a", "b") == 0 && other.ToLower() == "";
}
