namespace Constructs.Breaks;

// Issue #528. An `=` whose value is a single call on a receiver (no chain, one dot) breaks by a measured
// table (Fitter.HeldValueBreaks, SK-DIV-0331): behind a typed local only while the value fits below with
// three columns to spare; behind a `var` or assignment head under twelve columns while the value overflows
// below by at most one column or its `(` lands three short of the margin there; behind a longer head
// unless the call would move down at its dot as a chain's held first call does. Otherwise the `=` stays
// and the call's own dot breaks: `T c = JsonConvert` / `.DeserializeObject<T>(json);` (Newtonsoft).
public class HeldSingleCall {
    void M() {
        PrivateConstructorTestClass c = JsonConvert.DeserializeObject<PrivateConstructorTestClass>(json);
        PrivateConstructorWithPublicParameterizedConstructorTestClass c2 = JsonConvert.DeserializeObject<PrivateConstructorWithPublicParameterizedConstructorTestClass>(json);
        var y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggg>(json);
        y = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggg>(json);
        Taaaaaaaaaa c = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggg>(json);
        var zzzzzzzzzzzz = Jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = Jjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(alpha);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(alpha);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(alpha);
        var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(alpha);
        var y = sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(alpha);
        var w = Ssssssssssssssssssssssssssssssssss.Selecttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt(alpha, beta);
        var wwwwwwwwwwww = Ssssssssssssssssssssssssssssssssss.Selecttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt(alpha, beta);
    }
}
