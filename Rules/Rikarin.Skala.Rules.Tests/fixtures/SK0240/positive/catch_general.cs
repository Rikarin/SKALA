class C {
    public static void Save() {
        try {
            Run();
            Flush();
        } catch {
            throw;
        }
    }

    static void Run() { }

    static void Flush() { }
}
