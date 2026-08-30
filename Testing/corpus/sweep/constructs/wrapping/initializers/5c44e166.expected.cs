// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
using System.Collections.Generic;

class Initializers {
    void Fits() {
        var a = new List<int> {
            1,
            2,
            3
        };
        var b = new Thing {
            Alpha = 1,
            Beta = 2
        };
    }

    void BrokenInSourceButFits() {
        var c = new Thing {
            Alpha = 1,
            Beta = 2
        };
    }

    void OverTheElementCap() {
        var d = new List<int> {
            1,
            2,
            3,
            4,
            5
        };
        var e = new[] { 1, 2, 3, 4, 5 };
    }

    void ArrayFills() {
        var f = new[] {
            "aaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbb", "ccccccccccccccc", "ddddddddddddddd", "eeeeeeeeeeeeeee",
            "fffffffffffffff"
        };
    }

    void ObjectChopsWhenTheElementsDoNotShareALine() {
        var g = new Thing {
            Alpha = "aaaaaaaaaaaaaaaaaaaaaa",
            Beta = "bbbbbbbbbbbbbbbbbbbbbbb",
            Gamma = "ccccccccccccccccccccccc",
            Delta = "d"
        };
    }

    void ObjectKeepsTheElementsTogetherWhenTheyShareALine() {
        var h = new Thing {
            Alpha = "aaaaaaaaaaaaaaaaaaaaaaaaa",
            Beta = "bbbbbbbbbbbbbbbbbbbbbbbbbb",
            Gamma = "cc"
        };
    }

    void AnonymousObject() {
        var i = new {
            AlphaMember = "aaaaaaaaaaaaaaaaaaaaaa",
            BetaMember = "bbbbbbbbbbbbbbbbbbbbbbb",
            GammaMember = "ccc"
        };
    }
}

class Thing {
    public object Alpha { get; set; }
    public object Beta { get; set; }
    public object Gamma { get; set; }
    public object Delta { get; set; }
}
