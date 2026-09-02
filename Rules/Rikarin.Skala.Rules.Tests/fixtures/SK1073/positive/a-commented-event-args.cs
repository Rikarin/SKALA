// ⚠ #302's shape (#325). The guard asked over the `new EventArgs()` node's FULL span, so a comment
// on the argument's own line — leading trivia of the `new` keyword — silenced the rule. The fix
// swaps the construction for `EventArgs.Empty` and never touches the line above it.
using System;

public sealed class Ticker {
    public event EventHandler? Tick;

    public void Run(int times) {
        for (var i = 0; i < times; i++) {
            Tick?.Invoke(
                this,
                // no payload to carry for this event
                new EventArgs()
            );
        }
    }
}
