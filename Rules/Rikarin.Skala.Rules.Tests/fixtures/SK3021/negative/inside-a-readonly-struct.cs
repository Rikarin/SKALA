using System.Threading;

public readonly struct Guarded {
    // ⚠ The same bug without the keyword: a `readonly struct`'s fields are implicitly readonly, so
    // the copy happens anyway and there is no `readonly` on the field to delete. The repair is to
    // stop the containing type being a `readonly struct`, which is a design change rather than an
    // edit, and the rule has none.
    readonly SpinLock gate;

    public Guarded(bool trackThreadIds) => gate = new(trackThreadIds);

    public bool Try() {
        var taken = false;
        var local = gate;
        local.Enter(ref taken);
        if (taken) {
            local.Exit();
        }

        return taken;
    }
}
