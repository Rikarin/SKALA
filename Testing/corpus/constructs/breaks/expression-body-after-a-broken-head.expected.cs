// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
namespace Constructs.Breaks;

// SK-DIV-0113 (issue #372), and SK-DIV-0098 with it. `place_expr_method_on_single_line =
// if_owner_is_single_line` reads the *output*: the arrow of an expression body breaks whenever any
// break before it was taken — a filled type parameter list, a chopped parameter list, a `where`
// clause moved down, a kept break after a modifier or before the method's name — whether or not
// `=> body;` would have fit on the head's last line. Before this file Skala read one list's group
// and the source instead, so the fill's break was invisible on pass one and read as the author's
// on pass two: the Nightly fuzzer's seed 7611825995831206751 kept `=> builder.Items;` inline and
// then moved it. Attributes on their own line are not part of the head; the one-line members at
// the end stay whole.
public sealed record ExpressionBodyAfterABrokenHead : IDisposable, IEnumerable<object> {
    private IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>> M113<T110, T111,
        T112>() where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
        builder.Items;

    private IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>> M114<T110, T111,
        T112>() where T111 : notnull, IEquatable<T111> =>
        builder.Items;

    private IReadOnlyDictionary<object, object> M115<T110, T111, T112>()
        where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
        builder.Items;

    private IReadOnlyDictionary<object, object> M116<T110, T111, T112>()
        where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
        builder.ItemsWithAVeryLongName.SelectMany(x => x);

    private IReadOnlyDictionary<object, object> M117<T110, T111,
        T112>() where T111 : notnull =>
        builder.Items;

    private IReadOnlyDictionary<object, object> M118<T110, T111, T112>()
        where T111 : notnull
        where T112 : unmanaged =>
        builder.Items;

    private IReadOnlyDictionary<object, object> M119<T110, T111, T112>(
        int a,
        int b
    ) where T111 : notnull =>
        builder.Items;

    private IReadOnlyDictionary<object, object>
        M120<T110, T111, T112>() where T111 : notnull =>
        builder.Items;

    public
        int O() =>
        1;

    public ExpressionBodyAfterABrokenHead(
        int aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
        int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
        int cccccccccccccccccccc
    ) =>
        builder.Items = 1;

    void Q() {
        int Local<T110, T111,
            T112>() where T111 : notnull =>
            1;
    }

    [Obsolete]
    public int M() => 1;

    [Obsolete]
    public int N() => 1;

    private IReadOnlyDictionary<object, object> Short<T110>() where T110 : notnull => builder.Items;

    private IReadOnlyDictionary<object, object> Two<T110>() where T110 : class, new() => builder.Items;

    public int P {
        get => 1;
    }
}
