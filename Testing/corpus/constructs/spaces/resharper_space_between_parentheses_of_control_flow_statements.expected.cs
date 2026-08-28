// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-28
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
