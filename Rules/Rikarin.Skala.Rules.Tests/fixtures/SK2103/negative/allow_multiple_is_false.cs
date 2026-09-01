using System;

// ⚠ An attribute that does not allow multiples cannot legally repeat — a second application is
// CS0579 — so this rule has nothing to add and never considers one.
[AttributeUsage(AttributeTargets.Class)]
sealed class SingleAttribute : Attribute { }

[Single]
sealed class Ledger { }
