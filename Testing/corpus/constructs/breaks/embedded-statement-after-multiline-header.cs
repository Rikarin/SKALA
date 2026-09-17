namespace Constructs.Breaks;

// SK-DIV-0106 (issue #370). Under keep_existing_embedded_arrangement a simple embedded statement
// leaves its header's closing line only when it does not fit there: a header the author broke after
// the `(` or before an operator keeps its statement on the `)` line, a header the margin chops keeps
// it too, and the statement moves only when what is left of the line has no room for it — in which
// case the header stays whole. Before this file Skala read any multi-line header as "the owner does
// not fit on one line" and pushed the statement off.
public class EmbeddedStatementAfterMultilineHeader {
    void KeptHeaderBreaks(bool c, int n, int[] xs) {
        while (
            c) n++;

        if (
            c) n++;

        foreach (
            var x in xs) n++;

        for (
            var i = 0; i < n; i++) n++;

        lock (
            xs) n++;

        while (c
            && n > 0) n--;

        if (c
            && n > 0) n--;

        if (
            c) n++;
        else n--;

        foreach (var x in xs
            .Where(v => v > 0)) n++;
    }

    void HeaderChoppedForWidth(bool c, int n, int[] xs) {
        while (
            c && n > 0 && xs.Length > 0 && xs[0] > 0 && xs[1] > 0 && xs[2] > 0 && xs[3] > 0 && xs[4] > 0 && n < 1000000) n++;

        while (
            c && n > 0 && xs.Length > 0 && xs[0] > 0 && xs[1] > 0 && xs[2] > 0 && xs[3] > 0 && xs[4] > 0 && n < 10) n++;

        while (c && n > 0 && xs.Length > 0 && xs[0] > 0 && xs[1] > 0 && xs[2] > 0 && xs[3] > 0 && xs[4] > 0 && n < 100000000) n++;
    }

    void StatementPushedOff(bool c, bool d, int n, int[] xs, int depth) {
        if (depth < 0) throw new System.InvalidOperationException("a message long enough to run the whole line past the margin");

        if (c.ToString().Length > 0 && d.ToString().Length > 0) throw new System.InvalidOperationException("a message long enough");

        while (c && d) Frobnicate(xs.Length, xs.Length, xs.Length, xs.Length, xs.Length, xs.Length, xs.Length, xs.Length);

        if (c) Frobnicate(xs.Length, xs.Length, xs.Length, xs.Length, xs.Length, xs.Length, xs.Length, xs.Length, xs.Length);
    }

    void AuthorsOwnBreak(bool c, int n) {
        while (
            c)
            n++;

        while (c)
            n++;

        while (c) n++;
    }

    void Frobnicate(params int[] values) { }
}
