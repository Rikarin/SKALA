// The other thing `@` means. None of these is an identifier token.
class C {
    string M(string tail) {
        var path = @"C:\temp\log";
        var joined = @$"{path}\{tail}";
        return path + joined;
    }
}
