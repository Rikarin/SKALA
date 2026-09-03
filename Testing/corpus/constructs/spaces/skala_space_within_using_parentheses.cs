class C {
    void M(System.IDisposable d) {
        using (d) {
            System.Console.Write(d);
        }
    }
}
