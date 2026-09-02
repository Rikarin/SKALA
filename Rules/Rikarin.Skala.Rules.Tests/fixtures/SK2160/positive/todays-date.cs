using System;

public sealed class Ledger {
    // `Today` is `Now.Date` and carries the whole of `Now`'s dependency on the machine.
    public bool IsDueToday(DateTime due) => due.Date == DateTime.Today;
}
