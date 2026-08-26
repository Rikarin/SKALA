// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
class ExpressionBodies {
    int Method() => 1;

    int Property => 2;

    int Accessor {
        get => 3;
        set => _field = value;
    }

    int JoinedAlready => 4;

    int TooLongForOneLine => ComputeSomething(firstArgumentName, secondArgumentName, thirdArgumentName, fourthArgument);
}
