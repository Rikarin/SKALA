struct Span2(int start, int end) {
    public void Swap() => (start, end) = (end, start);

    public int Length => end - start;
}
