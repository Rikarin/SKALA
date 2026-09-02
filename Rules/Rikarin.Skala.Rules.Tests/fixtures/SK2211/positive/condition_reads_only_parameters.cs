class C {
    void M(bool waiting, int budget) {
        while (waiting && budget > 0) {
            System.Console.WriteLine(budget);
        }
    }
}
