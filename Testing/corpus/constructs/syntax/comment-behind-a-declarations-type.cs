// A block comment between a declaration's type and its first name breaks the line after the comment,
// and the name goes one continuation level in (#420). Skala kept the line whole. It is the shape of a
// local, a field, an event field and a using statement's resource; a comment anywhere else in a
// declaration head — between modifiers, before the type, after the name, before a second declarator,
// in a for or fixed statement, after foreach's var, in an out var or a pattern, before a property's,
// method's, local function's or parameter's name — stays on the line, and those are the controls.
// The declarator list stays whole behind the broken head, an `=` that breaks after it takes a second
// level, and an author's break before the comment puts the comment on a continuation line too.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CommentBehindADeclarationsType {
    int /*f01*/f1 = 1;
    public /*f02*/ int f2 = 1;
    int f4 /*f04*/ = 1;
    const int /*f07*/ f7 = 1;
    int /*f08*/ P1 { get; set; }
    event Action /*f10*/ E1;
    List<int> /*f11*/ f11;
    int /*f12*/ a12, b12;
    int
        /*g02*/ g2 = 1;

    void /*m01*/ A1() { }
    void A4(int /*m04*/ x, int y) { }

    unsafe void Unsafe(int[] arr) {
        fixed (int* /*u01*/ p = arr) { }
        using (IDisposable /*u02*/ d = null) { }
        int* /*u03*/ q = null;
    }

    async Task Locals(object o, int[] arr) {
        var /*k01*/v1 = 1;
        int /*k02*/ v2 = 1;
        (int, string) /*k03*/ v3 = (1, "");
        Func<int> /*k04*/ v4 = null;
        int? /*k05*/v5 = 1;
        int v6 /*k06*/ = 1;
        const /*k08*/ int v8 = 1;
        int a11, /*k11*/ b11;
        await using var /*a01*/ x = (IAsyncDisposable)null;
        foreach (var /*k14*/ e in arr) { }
        for (int /*k15*/ i = 0; i < 1; i++) { }
        M(out var /*k16*/ v16);
        if (o is int /*k17*/ v17) { }
        int /*k21*/ Local(int q) => q;
        ref int /*k22*/ v22 = ref arr[0];
        var /*k25a*/ /*k25b*/ v25 = 1;
        string /*h04*/ s = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var /*d01*/ t = arr.Length > 0
            ? arr[0]
            : 1;
    }

    void M(out int x) { x = 0; }
}
