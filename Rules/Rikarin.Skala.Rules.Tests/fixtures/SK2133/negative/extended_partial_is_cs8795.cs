// ⚠ The half of issue #186 that is a compile error rather than a rule. A C# 9 extended partial
// method — accessibility modifier, non-void return, `out` parameter — *must* have an implementation;
// without one it is CS8795 and never reaches a compiling analysis at all. Here it is implemented,
// because the unimplemented form could not be a fixture: it would not compile.
partial class Importer {
    public partial int Count();
}

partial class Importer {
    public partial int Count() => 3;
}
