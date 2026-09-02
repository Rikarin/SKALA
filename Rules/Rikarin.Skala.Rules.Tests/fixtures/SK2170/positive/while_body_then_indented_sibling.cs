class C {
    void M(int[] data) {
        var total = 0;
        var i = 0;
        while (i < data.Length)
            total += data[i];
            i++;
        Use(total, i);
    }

    static void Use(int total, int index) { }
}
