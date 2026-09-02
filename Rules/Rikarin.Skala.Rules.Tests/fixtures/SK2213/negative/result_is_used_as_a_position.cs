// The result is a position here, not a presence test, and nothing compares it to zero at all.
class C {
    string Scheme(string path) {
        var colon = path.IndexOf(':');
        return colon > 0 ? path.Substring(0, colon) : path;
    }
}
