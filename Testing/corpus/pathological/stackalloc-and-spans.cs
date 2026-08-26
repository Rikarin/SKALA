class C {
    void M() {
        System.Span<int> s = stackalloc int[4];
        s[0] = 1;
    }
}
