using System;

// The left operand is not a delegate, and nothing here is an unsubscription.
public sealed class Ledger {
    int balance;

    public void Withdraw(int amount) {
        balance -= amount;
    }

    public Func<int> Balance => () => balance;
}
