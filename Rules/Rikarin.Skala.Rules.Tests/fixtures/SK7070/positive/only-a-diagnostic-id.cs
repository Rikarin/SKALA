using System;

public sealed class Store {
    // ⚠ `DiagnosticId` makes the warning suppressible by a stable id; it is not shown to the
    // caller, so it is not a replacement for the message.
    [Obsolete(DiagnosticId = "STORE001", UrlFormat = "https://example.invalid/{0}")]
    public void Save() { }
}
