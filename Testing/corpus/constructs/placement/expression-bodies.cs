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
