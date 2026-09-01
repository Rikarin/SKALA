sealed class ReportFixture {
    readonly string title = "report";

    public string Line() => Format(1) + title;

    string Format(int count) => count + " rows";
}
