public sealed class Progress {
    public string Line(int done, int total) => string.Format("{0} of {1}", done, total);
}
