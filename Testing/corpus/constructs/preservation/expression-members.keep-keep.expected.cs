// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class ExpressionMembers {
    int ShortProperty =>
        1;

    int ShortMethod() =>
        2;

    int Accessors {
        get =>
            3;
    }

    int AlreadyOnOneLine => 4;

    string TooLongToJoin =>
        string.Join(", ", "a rather long first piece", "a rather long second piece", "and a third");
}
