// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class ExpressionBodies {
    int Method() => 1;

    int Property => 2;

    int Accessor {
        get =>
            3;
        set =>
            _field = value;
    }

    int JoinedAlready => 4;

    int TooLongForOneLine => ComputeSomething(firstArgumentName, secondArgumentName, thirdArgumentName, fourthArgument);
}
