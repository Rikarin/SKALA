// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A plain member access past the margin — `receiver.Property`, no call in it — as a `return`'s value, a
// local's and an assignment's (#446, SK-DIV-0210/0124): the oracle breaks before the last dot, one level
// in, whatever the receiver's width, and never after the `=`. A line that fits stays whole.

class MemberAccessLastDot {
    object M() {
        return r.Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return r
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return r
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return r
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrr.Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = r.Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = r
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = r
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = r
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrr.Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        var x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = r.Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = r
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = r
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = r
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrr.Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        _x = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
            .Ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp;
        return alpha.beta.gamma
            .Deltadddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd;
        return null;
    }
}
