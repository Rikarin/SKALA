// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
using System;
using System.Collections.Generic;

class C {
    int _n;

    int P {
        get { return _n; }
    }

    int Q {
        get { return _n; }
        set { _n = value; }
    }

    int R { get; set; }

    int S {
        get => _n;
    }

    void M() {
        A();
    }

    void Empty() { }

    void N(bool c, object o) {
        Register(() => { A(); });
        Register(delegate { A(); });
        Action a = () => { A(); };
        Action b = delegate { A(); };
        Func<int, int> f = x => { return x; };
        if (c) {
            A();
        }

        while (c) {
            A();
        }

        void L() {
            A();
        }

        var l = new List<int> { 1, 2 };
        var x = new { X = 1 };
        var s = _n switch {
            _ => 1
        };
        var t = o is string { Length: 1 };
        if (c) { }

        Register(() => { });
    }

    void A() { }
    void Register(Action a) { }
}
