// A break the author kept before an `is` ahead of a binary pattern (#446): the `is` line takes a level
// past the operand's own line, as a type test's does, and the combinators stay on its column — under an
// expression body, after `return` and in a local's value.
class KeptBreakBeforeIs {
    static bool IsStringText(Token token) =>
        token.Kind()
        is SyntaxKind.InterpolatedStringStartToken
        or SyntaxKind.InterpolatedVerbatimStringStartToken
        or SyntaxKind.InterpolatedStringEndToken;

    static bool Other(Token token) {
        var text = token.Kind()
            is SyntaxKind.OpenParenToken
            or SyntaxKind.CloseParenToken;
        return token.Kind()
            is SyntaxKind.OpenBracketToken
            or SyntaxKind.CloseBracketToken;
    }
}
