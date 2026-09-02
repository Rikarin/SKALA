// ⚠ The stated hole, written down as a fixture so it cannot be lost. `DayOfWeek` is numbered 0..6
// and carries no `[Flags]`, and combining its members is exactly the defect — but the rule reads
// the declaration's syntax for its evidence, and an enum from a referenced assembly has none.
// Reporting it would mean inferring intent from the constants, which cannot distinguish a
// three-member choice from a two-flag bit set.
using System;

sealed class Schedule {
    public DayOfWeek Combine(DayOfWeek left, DayOfWeek right) => left | right;

    public DayOfWeek Invert(DayOfWeek day) => ~day;
}
