// There is no statement to delete, and the rule's fix is a deletion.
using System;

sealed class Terse : IDisposable {
    public void Dispose() => GC.SuppressFinalize(this);
}
