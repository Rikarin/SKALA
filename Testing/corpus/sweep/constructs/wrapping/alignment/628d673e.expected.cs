// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
public class Alignment {
    // The `align_multiline_*` family: whether a wrapped construct's continuation lines take an
    // indent level from the line they are on, or a column from the construct's own first token.
    // Every key is false in the export, so this fixture is the indent-level shape and the option
    // units are what flip each construct to its column.
    void ObjectInitializer() {
        var value = new SomeTypeWithALongName {
            FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 333
        };
    }

    void ArrayInitializer() {
        var value = new[] {
            "aaaaaaaaaaaaa", "bbbbbbbbbbbbb", "ccccccccccccccc", "ddddddddddddd", "eeeeeeeeeeeee", "ffff"
        };
    }

    void AnonymousObject() {
        var value = new {
            FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 3, FourthPropertyName = 44
        };
    }

    void ListPattern(object candidate) {
        var matched = candidate is [
            firstElementPatternName, secondElementPatternName, thirdElementPatternName, fourthElementPatternName,
            fifthElementPatternName
        ];
    }

    void CollectionExpression() {
        string[] value = [
            "aaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbb", "ccccccccccccccccccc", "ddddddddddddddddddd",
            "eeeeeeeeeeeeeeeeeee", "fffffffffffffffffff"
        ];
    }

    void PropertyPattern(object candidate) {
        var matched = candidate is {
            OnlySubpatternPropertyName: "a string long enough that the pattern cannot stay on its line"
        };
    }

    void SwitchExpression(int value) {
        var text = value switch {
                       1 => "oneoneoneoneone",
                       2 => "twotwotwotwotwo",
                       3 => "threethreethree",
                       _ => "zzzzzzzz"
                   };
    }

    void BinaryChain() {
        var total = someLongVariableName
            + anotherLongVariableName
            + yetAnotherLongName
            + oneMoreLongVariableNameHereXyz;
    }

    void PatternChain(object candidate) {
        var matched = candidate is SomeVeryLongTypeNameHere
            or AnotherVeryLongTypeNameHere
            or YetAnotherVeryLongTypeNameHere
            or FinalVeryLongTypeNameHere;
    }
}
