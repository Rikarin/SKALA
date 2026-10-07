// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
using System;

class OneStatementBlocks {
    int _n;
    Action _a;

    void OnOneLine() {
        A();
    }

    void Wraps() {
        _n = Math.Max(
            _n,
            1
        );
    }

    void PastTheMargin() {
        _n = _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n;
    }

    void HalfBroken() {
        A();
    }

    void TwoStatements() {
        A();
        B();
    }

    void Statements() {
        void Local() {
            A();
        }

        void LocalOfTwo() {
            A();
            B();
        }

        if (_n > 0) {
            A();
        }

        if (_n > 1) {
            _n = Math.Max(
                _n,
                1
            );
        }

        if (_n > 2) {
            A();
            B();
        }

        while (_n > 3) {
            _n = Math.Max(
                _n,
                1
            );
        }

        _a = () => { A(); };
        _a = () => { A(); };
        _a = () => { A(); };
        _a = () => {
            A();
            B();
        };
        _a = () => {
            _n = Math.Max(
                _n,
                1
            );
        };
    }

    void LocalAlone() {
        void LocalWraps() {
            _n = Math.Max(
                _n,
                1
            );
        }
    }

    public int Accessor {
        get { return _n; }
        set { _n = value; }
    }

    public int AccessorBroken {
        get { return _n; }
    }

    public int AccessorWraps {
        get {
            return Math.Max(
                _n,
                1
            );
        }
    }

    void A() { }

    void B() { }
}
