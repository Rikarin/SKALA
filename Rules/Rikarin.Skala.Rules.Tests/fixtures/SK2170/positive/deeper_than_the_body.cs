class C {
    void M(int[] data) {
        foreach (var value in data)
            Record(value);
                Flush();
    }

    static void Record(int value) { }

    static void Flush() { }
}
