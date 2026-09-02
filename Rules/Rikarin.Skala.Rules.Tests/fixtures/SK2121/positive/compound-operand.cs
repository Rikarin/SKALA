// ⚠ The parenthesisation case. A cast binds tighter than `??`, so the fix has to keep the
// operand's own parentheses or `(Base)left ?? right` reassociates into something else.
class Base { }

sealed class Derived : Base { }

sealed class Consumer {
    public Base? Widen(Derived left, Derived right) => (left ?? right) as Base;
}
