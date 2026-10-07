// #403: `abstract` on an explicit member re-abstracts the base interface's default. Without it the
// declaration is a default implementation with no body — CS0501 — so the keyword is load-bearing.
interface IReader {
    int Read() => 0;
}

interface IStrictReader : IReader {
    abstract int IReader.Read();
}
