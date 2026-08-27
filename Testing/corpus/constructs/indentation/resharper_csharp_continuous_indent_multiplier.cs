class C {
    int M(int a, int b) => a + b;

    // ⚠ A continuation the formatter keeps whatever else it does: the declaration does not fit on
    // one line, so the level the multiplier scales is spent here and the fixture stays a fixture.
    int N(int aVeryLongParameterName, int anotherVeryLongParameterName, int aThirdLongParameterName) =>
        aVeryLongParameterName + anotherVeryLongParameterName + aThirdLongParameterName;
}
