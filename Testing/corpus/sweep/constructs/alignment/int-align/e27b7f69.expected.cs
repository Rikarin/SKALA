// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
public class IntAlignFixture {
    // The `int_align_*` family: adjacent constructs of the same kind padded so that one token of
    // each lands in the same column. Every key is false in the export, so this fixture is the
    // unpadded shape and the option units are what pad it.
    int first = 1;
    string secondFieldName = "x";
    double third = 3.5;

    public int Short { get; set; }
    public string LongerPropertyName { get; set; }

    void Method(int a)                { }
    void MethodWithALongerName(int a) { }

    void Variables() {
        var one = 1;
        var somewhatLonger = 2;
        var two = 3;
    }

    void Assignments(int one, int somewhatLonger, int two) {
        one = 10;
        somewhatLonger = 200;
        two = 3000;
    }

    void Comments() {
        var one = 1; // the first
        var somewhatLonger = 2; // the second
        var two = 3; // the third
    }

    int Sections(int value) {
        switch (value) {
            case 1: return 10;
            case 22: return 200;
            case 333: return 3000;
        }

        return 0;
    }

    string Arms(int value) =>
        value switch {
            1 => "one",
            22 => "twentytwo",
            333 => "threehundred",
            _ => "none"
        };

    enum Members {
        A = 1,
        Bbbbbb = 2,
        Cc = 3
    }
}
