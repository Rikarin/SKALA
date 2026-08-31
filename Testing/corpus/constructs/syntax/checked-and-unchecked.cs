using System;

// CheckedStatement occurred nowhere; UncheckedStatement, CheckedExpression and UncheckedExpression
// occurred once or twice each, and no checked *operator* declaration existed at all. `checked { }` is
// a braced block that is not a control-flow statement, which is a shape
// `resharper_csharp_indent_braces` and the brace-placement keys have no other example of; the
// parenthesised form is what `resharper_csharp_space_before_checked_parentheses` is about.
struct Money {
    public readonly long Cents;

    public Money(long cents) => Cents = cents;

    public static Money operator +(Money left, Money right) => new Money(left.Cents + right.Cents);

    public static Money operator checked +(Money left, Money right) => new Money(checked(left.Cents + right.Cents));

    public static explicit operator int(Money money) => (int)money.Cents;

    public static explicit operator checked int(Money money) => checked((int)money.Cents);
}

class CheckedAndUnchecked {
    static int Statements(int alpha, int bravo) {
        checked {
            alpha += bravo;
        }

        unchecked {
            alpha *= bravo;
        }

        checked {
            unchecked {
                return alpha - bravo;
            }
        }
    }

    static long Expressions(int alpha, int bravo) =>
        checked(alpha + bravo) + unchecked(alpha * bravo) + checked((long)alpha << bravo);

    static long Overflowing(int alpha, int bravo, int charlie, int delta) {
        checked {
            return alpha * bravo * charlie * delta + alpha * bravo + charlie * delta + alpha * charlie + bravo * delta;
        }
    }
}
