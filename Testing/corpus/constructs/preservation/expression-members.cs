class ExpressionMembers {
    int ShortProperty => 1;

    int ShortMethod() => 2;

    int Accessors {
        get => 3;
    }

    int AlreadyOnOneLine => 4;

    string TooLongToJoin => string.Join(", ", "a rather long first piece", "a rather long second piece", "and a third");
}
