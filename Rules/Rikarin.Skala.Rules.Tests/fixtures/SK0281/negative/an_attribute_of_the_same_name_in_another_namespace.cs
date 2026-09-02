namespace Local {
    class SetsRequiredMembersAttribute : System.Attribute { }

    class Options {
        [SetsRequiredMembers]
        public Options(string path) => Path = path;

        public string Path { get; init; }
    }
}
