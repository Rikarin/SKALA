class C {
    void M(bool b, int[] xs, object o) {
        if (b) {
            M(b, xs, o);
        }

        while (b) {
            M(b, xs, o);
        }

        foreach (var x in xs) {
            M(b, xs, o);
        }

        lock (o) {
            M(b, xs, o);
        }

        switch (b) {
            case true:
                break;
        }
    }
}
