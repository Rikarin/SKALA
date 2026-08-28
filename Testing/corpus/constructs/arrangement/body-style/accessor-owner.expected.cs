// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
using System;

namespace Skala.Corpus.Arrangement;

// accessor_owner_body = expression_body has two shapes and the key names only one of them: a
// get-only property collapses onto the PROPERTY, a property with more than one accessor keeps its
// accessor list and each accessor gets the expression body.
public class AccessorOwner {
    private int _n;
    private string _text = "";

    public int GetOnly {
        get { return _n; }
    }

    public int GetAndSet {
        get { return _n; }
        set { _n = value; }
    }

    public string Computed {
        get { return _text.Trim(); }
    }

    public int this[int index] {
        get { return _n + index; }
    }

    // An accessor with two statements is not a candidate; the property keeps its block.
    public int Guarded {
        get {
            Console.WriteLine("read");
            return _n;
        }
    }

    // An accessor with an attribute or a modifier is left alone by the owner collapse.
    public int Restricted {
        get { return _n; }
        private set { _n = value; }
    }

    public int LocalFunctionOwner() {
        int Add(int x) {
            return x + _n;
        }

        return Add(1);
    }
}
