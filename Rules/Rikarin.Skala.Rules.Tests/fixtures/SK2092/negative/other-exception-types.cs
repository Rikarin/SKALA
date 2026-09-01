using System;
using System.IO;

sealed class Reader {
    // The neighbours that read similarly and are not it. `ArgumentNullException` in particular is a
    // contract being enforced, not a dereference that already happened.
    public void Handles(string? path) {
        try {
            Load(path!);
        } catch (ArgumentNullException) {
            Load("default.json");
        }

        try {
            Load(path!);
        } catch (NullReferenceExceptionHandler.Marker) {
            Load("default.json");
        }

        try {
            Load(path!);
        } catch (IOException) {
            Load("default.json");
        }

        try {
            Load(path!);
        } catch (InvalidOperationException) {
            Load("default.json");
        }
    }

    static void Load(string path) { }
}

// A type whose *containing* name mentions the framework one. The match is on the type the clause
// names, not on the text appearing anywhere in it.
static class NullReferenceExceptionHandler {
    public sealed class Marker : Exception;
}
