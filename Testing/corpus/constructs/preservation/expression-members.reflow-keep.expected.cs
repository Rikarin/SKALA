// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
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
