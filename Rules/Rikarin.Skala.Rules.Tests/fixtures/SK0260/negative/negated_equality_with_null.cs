// `SK1010` owns this span: it reports `text == null` as `text is null`. One span, one id.
class C {
    public static bool Run(string? text) => !(text == null);
}
