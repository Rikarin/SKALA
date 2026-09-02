// ⚠ The finding is withdrawn rather than the fix, so no positive fixture can produce a report the
// fix cannot serve. The fix replaces the whole `not { }` span, and a fix that silently deleted this
// comment out of it would be a fix nobody can review.
class C {
    bool M(object? result) =>
        result is not /* deliberately spelled this way, see #1234 */ { };
}
