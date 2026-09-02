class C {
    bool Last(string path) => path.LastIndexOf('/') > 0;

    bool Any(string path) => path.IndexOfAny(new[] { '/', '\\' }) > 0;
}
