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
