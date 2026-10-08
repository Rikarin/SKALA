// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #559: a filled positional pattern breaks between a declaration's type and its name when the type fits and the name does not.

class C559f {
    int A(object owner) =>
        owner switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccc, int
                dddd) => 1,
            _ => 0
        };

    int B(object owner) =>
        owner switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccc,
                int d) => 1,
            _ => 0
        };

    int C(object owner) =>
        owner switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccc, int
                ddddddddddddddd) => 1,
            _ => 0
        };

    int D(object owner) =>
        owner switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccc,
                string dddd) => 1,
            _ => 0
        };

    int E(object owner) =>
        owner switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccc, var
                dddd) => 1,
            _ => 0
        };

    int G(object owner) =>
        owner switch {
            (aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccccc, int
                ddddddddddddd) => 1,
            _ => 0
        };

    void H(object owner) {
        switch (owner) {
            case (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccc,
                int dddd):
                break;
        }

        var (aaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccc, ddddddd) =
            Get();
    }
}
