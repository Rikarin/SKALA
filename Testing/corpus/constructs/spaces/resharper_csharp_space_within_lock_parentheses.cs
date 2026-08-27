class C {
    void M(object o) {
        lock (o) {
            System.Console.Write(o);
        }
    }
}
