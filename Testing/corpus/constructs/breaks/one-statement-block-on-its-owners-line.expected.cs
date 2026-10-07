// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// A one-statement block that may share its owner's line — an accessor's, a lambda's, an anonymous
// method's — stays there exactly when its statement ends up on that line too (issue #405). Nothing
// in the corpus wrote one whose statement wraps, so the sweep never saw that Skala kept every such
// block on its line while the oracle breaks it open: an argument list the author chopped, a binary
// the author broke, a switch expression that always chops, a sum past the margin, a lambda block of
// its own that broke. A broken block whose statement fits is joined, an initializer that re-joins
// leaves its block alone, and two statements are always one per line. An anonymous method that
// breaks is an ordinary argument, not the single lambda that keeps the call's line, and a block
// whose owner's head spans lines — a parameter list the author broke — breaks open with it.

using System;
using System.Collections.Generic;

class Accessors {
    int _n;
    Func<int> _f;
    Action _a;
    event EventHandler _e;

    public int Args {
        get {
            return Math.Max(
                _n,
                1
            );
        }
    }

    public int Binary {
        get {
            return _n
                + 1;
        }
    }

    public int Switch {
        get {
            return _n switch {
                _ => 0
            };
        }
    }

    public int SetArgs {
        get { return _n; }
        set {
            _n = Math.Max(
                value,
                1
            );
        }
    }

    public int InitArgs {
        get { return _n; }
        init {
            _n = Math.Max(
                value,
                1
            );
        }
    }

    public event EventHandler Event {
        add {
            _e += Combine(
                value,
                value
            );
        }
        remove { _e -= value; }
    }

    public int LambdaBlockOfTwo {
        get { return _n; }
        set {
            _f = () => {
                _n = 1;
                return value;
            };
        }
    }

    public int LambdaBlockOfOne {
        get { return _n; }
        set { _f = () => { return value; }; }
    }

    public int LambdaBodyBroken {
        get { return _n; }
        set {
            _f = () =>
                value;
        }
    }

    public List<int> InitializerRejoins {
        get { return new List<int> { 1, 2 }; }
    }

    public int PastTheMargin {
        get {
            return _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n
                + _n;
        }
    }

    public int FitsAtTheMargin {
        get { return Math.Max(_n + _n + _n + _n + _n + _n + _n + _n + _n + _n, _n + _n + _n + _n + _n + _n + _n + _n); }
    }

    public int OneColumnPast {
        get {
            return Math.Max(_n + _n + _n + _n + _n + _n + _n + _n + _n + _n, _n + _n + _n + _n + _n + _n + _n + 100);
        }
    }

    public int BrokenAndFits {
        get { return _n; }
    }

    public int BrokenAfterTheBrace {
        get { return _n; }
    }

    public int BrokenBeforeTheBrace {
        get { return _n; }
    }

    public int TwoStatements {
        get { return _n; }
        set {
            _n = value;
            _n++;
        }
    }

    static EventHandler Combine(EventHandler a, EventHandler b) => a;
}

class Lambdas {
    int _n;
    Func<int> _f;
    Action _a;

    void Register(Action a) { }

    void Register(int x, Action a) { }

    void Use(Func<int, int> f) { }

    void Blocks() {
        _a = () => { A(); };
        _a = () => { A(); };
        _a = () => {
            A();
            B();
        };
        Register(() => { A(); });
        Register(() => {
                A();
                B();
            }
        );
        Register(delegate { A(); });
        Register(
            delegate {
                A();
                B();
            }
        );
        Register(
            1,
            delegate() {
                A();
                B();
            }
        );
        Use(
            delegate(
                int first
            ) {
                return first;
            }
        );
        Use((
                int first
            ) => {
                return first;
            }
        );
        _a = () => {
            _n = Math.Max(
                _n,
                1
            );
        };
        Register(() => {
                _n = Math.Max(
                    _n,
                    1
                );
            }
        );
        _a = () => {
            _n = _n
                + 1;
        };
        _f = () => {
            return _n switch {
                _ => 0
            };
        };
        _a = () => {
            _a = () => {
                A();
                B();
            };
        };
        _a = delegate { A(); };
        _a = delegate {
            _n = Math.Max(
                _n,
                1
            );
        };
    }

    void A() { }

    void B() { }
}

class Outer {
    class Inner {
        int _n;
        Action _a;

        public int Args {
            get {
                return Math.Max(
                    _n,
                    1
                );
            }
            set {
                _a = () => {
                    _n = Math.Max(
                        value,
                        1
                    );
                };
            }
        }

        public int Fits {
            get { return _n; }
            set { _a = () => { _n = value; }; }
        }

        void M() {
            _a = () => {
                _n = Math.Max(
                    _n,
                    1
                );
            };
        }
    }
}
