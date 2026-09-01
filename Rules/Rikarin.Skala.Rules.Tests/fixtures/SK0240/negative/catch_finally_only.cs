class C {
    public static void Save() {
        try {
            Run();
        } finally {
            Close();
        }
    }

    static void Run() { }

    static void Close() { }
}
