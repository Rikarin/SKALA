// ⚠ The exact shape that gave SK2009 twelve of its fourteen false positives on Skala's own
// source (#280), lifted from TaskReturnedFromUsingAnalyzer and AbstractTypeConstructorAnalyzer
// rather than invented: a switch statement over SyntaxKind used to pick a handful of modifiers
// out of a token list. Falling out of the switch means "this modifier is not interesting", which
// is the whole design — SyntaxKind has some 570 declared values and no caller ever means to list
// them.
//
// This fixture reaches the rule: the governing expression is an enum, the enum is not [Flags],
// no label is a catch-all, every label is a constant pattern, and members are missing. Only the
// `missing <= handled` comparison declines it, which is what makes it a test of the fix rather
// than a test of an early return.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

sealed class Filters {
    public static bool CannotBeAsync(SyntaxTokenList modifiers) {
        foreach (var modifier in modifiers) {
            switch (modifier.Kind()) {
                case SyntaxKind.AsyncKeyword:
                case SyntaxKind.PartialKeyword:
                case SyntaxKind.ExternKeyword:
                case SyntaxKind.AbstractKeyword:
                    return false;
            }
        }

        return true;
    }

    // The mixed-section variant, where the arms do *not* all produce the same value: one records
    // a token and lets the loop continue, the other abandons the member. #280 proposed "every arm
    // returns the same thing" as the boundary; this is the site that refutes it.
    public static bool DeclaredPublic(SyntaxTokenList modifiers) {
        var stated = false;
        foreach (var modifier in modifiers) {
            switch (modifier.Kind()) {
                case SyntaxKind.PublicKeyword:
                    return true;

                case SyntaxKind.PrivateKeyword:
                case SyntaxKind.ProtectedKeyword:
                case SyntaxKind.InternalKeyword:
                    stated = true;
                    break;
            }
        }

        return !stated;
    }

    // Not in a loop either, so "the switch is a loop body" does not separate it from the two
    // findings that survive on Skala's tree.
    public static int Weight(SyntaxNode node) {
        switch (node.Kind()) {
            case SyntaxKind.IfStatement:
            case SyntaxKind.WhileStatement:
            case SyntaxKind.ForEachStatement:
                return 1;

            case SyntaxKind.ForStatement:
                return 2;
        }

        return 0;
    }
}
