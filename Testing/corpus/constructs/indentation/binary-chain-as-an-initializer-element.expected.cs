// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class BinaryChainAsAnInitializerElement {
    // SK-DIV-0040. A binary expression chain has no continuation level of its own: it lands on the
    // one the construct around it opened. Where that construct is a braced initializer or a
    // collection expression, the element's own column IS that level, so the operators stay flush
    // with the element — and every other family in the same position takes one more.

    void ArrayInitializerElement() {
        var flags = new[] {
            FirstConditionValueLong
            && SecondConditionValueLong
            && ThirdConditionValueLong
            && FourthConditionValueLong
            && FifthCondition,
            SixthConditionValueLong
            && SeventhConditionValueLong
            && EighthConditionValueLong
            && NinthConditionValueLong
            && TenthCondition
        };
    }

    void CollectionExpressionElement() {
        bool[] flags = [
            FirstConditionValueLong
            && SecondConditionValueLong
            && ThirdConditionValueLong
            && FourthConditionValueLong
            && FifthCondition,
            SixthConditionValueLong
            && SeventhConditionValueLong
            && EighthConditionValueLong
            && NinthConditionValueLong
            && TenthCondition
        ];
    }

    void NestedInsideAnObjectInitializer() {
        var made = new Holder {
            Inner = new[] {
                FirstConditionValueLong
                && SecondConditionValueLong
                && ThirdConditionValueLong
                && FourthConditionValueLongs
            }
        };
    }

    // The controls. A chain the element merely contains takes the level, and so does every other
    // family in the element position — which is why the rule is about binary chains and not about
    // the position.

    void AssignmentElementKeepsTheLevel() {
        var made = new Holder {
            FirstPropertyName = FirstConditionValueLong
                + SecondConditionValueLong
                + ThirdConditionValueLong
                + FourthValueHere
        };
    }

    void TernaryElementKeepsTheLevel() {
        var picks = new[] {
            FirstConditionValueLong
                ? SecondConditionValueLong
                : ThirdConditionValueLong + NinthConditionValueLong + FourthConditionValueLongs
        };
    }

    void PatternElementKeepsTheLevel() {
        var picks = new[] {
            FirstConditionValueLong is SecondConditionValueLong
                or ThirdConditionValueLong
                or NinthConditionValueLong
                or FourthConditionValueLo
        };
    }

    void CallChainElementKeepsTheLevel() {
        var values = new[] {
            FirstProviderNameHere.SelectTheThing()
                .WhereTheOtherThingHappens()
                .OrderByDescendingTheThird()
                .ToArray()
                .Reverse()
        };
    }

    // And the delimited controls: a binary chain that is a parenthesis's whole content lands on the
    // parenthesis's level, while one that starts mid-line takes one more.

    void ArgumentElement() {
        Consume(
            FirstConditionValueLong
            && SecondConditionValueLong
            && ThirdConditionValueLong
            && FourthConditionValueLong
            && FifthCondition
        );
    }

    void NotTheWholeArgument() {
        Consume(
            (FirstConditionValueLong
                && SecondConditionValueLong
                && ThirdConditionValueLong
                && FourthConditionValueLong
                && FifthCondition)
        );
    }

    void StatementLevel() {
        var flag = FirstConditionValueLong
            && SecondConditionValueLong
            && ThirdConditionValueLong
            && FourthConditionValueLong
            && FifthCondition;
    }
}

class Holder {
    public object Inner { get; set; }
    public object FirstPropertyName { get; set; }
}
