// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    int _a;

    void M() {
        if (_a > 0) {
            _a = 1;
        }
    }

    int P {
        get { return _a; }
        set { _a = value; }
    }

    class Inner {
        int _b;
    }
}

enum E {
    A
}
