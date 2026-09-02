using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Analysis.Duplication;

/// <summary>
///     Token-level type-2 clone detection — <c>SK7020</c>, and the duplication percentage the
///     <c>metrics.duplication</c> gate reads.
/// </summary>
/// <remarks>
///     docs/plan/09 § "Duplication", verbatim: lex to a normalised token stream; roll a hash over windows
///     of <c>minTokens</c>; bucket by hash, <b>verify candidates exactly</b>, extend greedily in both
///     directions; report each maximal group once, at its first occurrence.
///     <para>
///         ⚠ The hash proposes; the comparison decides. A bucket is a list of <i>candidates</i> and every one
///         of them is compared token for token before it can become a finding, which is what
///         <c>rules.json</c> promises for <c>SK7020</c>: "the match is verified exactly rather than trusted
///         from the rolling hash, so a hash collision cannot produce a finding". Nothing in this file may be
///         changed in a way that lets a hash value reach the output.
///     </para>
///     <para>
///         ⚠ Cost is one pass and is bounded by I/O. The corpus is concatenated into one token array, hashed
///         once per window, and sorted — <c>O(n log n)</c> in tokens. Nothing here is quadratic in the number
///         of files, and the only <c>O(k²)</c>-looking step, comparing the members of a hash bucket, is a
///         sort by content rather than a pairwise scan.
///     </para>
///     <para>
///         ⚠ Determinism is explicit everywhere it could be accidental: files are ordered by path, buckets by
///         their first token position, groups by their first occurrence. Nothing reads a hash table's
///         enumeration order.
///     </para>
/// </remarks>
public static class CloneDetector {
    /// <summary>docs/plan/09: "windows of <c>minTokens</c> (default 100 ≈ 25 lines, Sonar's default for C#)".</summary>
    public const int DefaultMinTokens = 100;

    /// <summary>⚠ FNV's 64-bit prime. Odd, so multiplication modulo 2⁶⁴ is a bijection and no state is lost.</summary>
    const ulong Base = 0x100000001B3UL;

    /// <summary>How many other occurrences a finding's message names before it starts counting instead.</summary>
    const int NamedOccurrences = 10;

    /// <summary>
    ///     Measures one file set.
    /// </summary>
    /// <param name="files">
    ///     Every file to consider. Generated ones are dropped here, which is what takes them out of the
    ///     findings and out of both halves of the percentage.
    /// </param>
    /// <param name="minTokens">The shortest run that counts as a clone. <see cref="DefaultMinTokens" />.</param>
    /// <param name="cacheDirectory">
    ///     Where <c>clones.idx</c> lives, normally <c>&lt;root&gt;/.skala/cache</c>. Null disables the
    ///     persisted index; a corrupt or stale one degrades to a cold run and never to a wrong answer.
    /// </param>
    /// <param name="cancellation">Checked per file and per hash bucket.</param>
    public static DuplicationResult Detect(
        IReadOnlyList<DuplicationInput> files,
        int minTokens,
        string? cacheDirectory,
        CancellationToken cancellation
    ) =>
        Detect(files, minTokens, cacheDirectory, false, cancellation);

    /// <summary>
    ///     The test seam for the rule's central promise.
    /// </summary>
    /// <remarks>
    ///     ⚠ <paramref name="collapseHashes" /> gives every window the same hash — the worst bucket
    ///     collision there is — and the answer must come out identical, because bucketing only proposes
    ///     candidates. This is the only way to exercise the verification step without waiting for a real
    ///     2⁻⁶⁴ collision, and it is the property <c>SK7020</c>'s <c>falsePositives</c> field claims.
    /// </remarks>
    internal static DuplicationResult Detect(
        IReadOnlyList<DuplicationInput> files,
        int minTokens,
        string? cacheDirectory,
        bool collapseHashes,
        CancellationToken cancellation
    ) {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfLessThan(minTokens, 1);

        var measured = new List<DuplicationInput>(files.Count);
        foreach (var file in files) {
            if (!file.IsGenerated) {
                measured.Add(file);
            }
        }

        if (measured.Count == 0) {
            return new();
        }

        var index = cacheDirectory is null ? null : CloneIndex.Load(cacheDirectory);
        var lexed = new LexedFile[measured.Count];

        // ⚠ Written by index, never appended, so the parallel lex cannot reorder anything.
        Parallel.For(
            0,
            measured.Count,
            new ParallelOptions { CancellationToken = cancellation },
            i => {
                var input = measured[i];
                var hash = ContentHash.Of(input.Text);
                var tokens = index?.TryGet(input.Path, hash) ?? TokenStream.Lex(input.Text);
                index?.Put(input.Path, hash, tokens);
                lexed[i] = new() {
                    Path = input.Path, IsTest = input.IsTest, Text = SourceText.From(input.Text), Tokens = tokens
                };
            }
        );

        var production = Array.FindAll(lexed, static file => !file.IsTest);
        var tests = Array.FindAll(lexed, static file => file.IsTest);

        var (groups, duplicated) = Analyse(production, minTokens, collapseHashes, cancellation);
        var (testGroups, testDuplicated) = Analyse(tests, minTokens, collapseHashes, cancellation);

        index?.Save();

        return new() {
            Groups = groups,
            TestGroups = testGroups,
            DuplicatedLines = duplicated,
            TotalLines = TotalLines(production),
            TestDuplicatedLines = testDuplicated,
            TestTotalLines = TotalLines(tests)
        };
    }

    /// <summary>
    ///     Turns a result into <c>SK7020</c> findings: one per group, at the first occurrence, the rest
    ///     named in the message.
    /// </summary>
    /// <remarks>
    ///     ⚠ Production groups only. <c>rules.json</c>: test duplication "is often the readable choice and
    ///     gating it drives people to write worse tests" — a warning-severity finding in a test file is a
    ///     gate on test duplication however the gate is phrased. Tests are reported through
    ///     <see cref="DuplicationResult.TestPercentage" /> and <see cref="DuplicationResult.TestGroups" />,
    ///     beside the number rather than inside it.
    ///     <para>
    ///         ⚠ <c>Column</c> is 1 and <c>EndColumn</c> is 1: a duplicated block is a range of lines and the
    ///         column of its first token is not information anyone acts on. The exact span is in
    ///         <c>Start</c>/<c>Length</c>, which is what the fingerprint and the SARIF region use anyway.
    ///     </para>
    /// </remarks>
    /// <param name="result">
    ///     A result from <see cref="Detect(IReadOnlyList{DuplicationInput}, int, string?, CancellationToken)" />.
    /// </param>
    /// <param name="repositoryRoot">Only used to render the other occurrences relative in the message.</param>
    public static ImmutableArray<Finding> ToFindings(DuplicationResult result, string repositoryRoot) {
        ArgumentNullException.ThrowIfNull(result);

        var severity = Severity(RuleCatalog.Get(RuleIds.DuplicatedBlock).DefaultSeverity);
        var findings = ImmutableArray.CreateBuilder<Finding>(result.Groups.Length);
        foreach (var group in result.Groups) {
            var first = group.Occurrences[0];
            findings.Add(
                new Finding {
                    RuleId = RuleIds.DuplicatedBlock,
                    Severity = severity,
                    Message = Message(group, repositoryRoot),
                    Path = first.Path,
                    Line = first.StartLine,
                    Column = 1,
                    EndLine = first.EndLine,
                    EndColumn = 1,
                    Start = first.Start,
                    Length = first.Length
                }
            );
        }

        return findings.ToImmutable();
    }

    static string Message(CloneGroup group, string repositoryRoot) {
        var first = group.Occurrences[0];
        var builder = new StringBuilder("duplicated block of ")
            .Append(group.TokenLength.ToString(CultureInfo.InvariantCulture))
            .Append(" tokens (")
            .Append((first.EndLine - first.StartLine + 1).ToString(CultureInfo.InvariantCulture))
            .Append(" lines), also at ");

        var named = Math.Min(NamedOccurrences, group.Occurrences.Length - 1);
        for (var i = 1; i <= named; i++) {
            var occurrence = group.Occurrences[i];
            if (i > 1) {
                builder.Append(", ");
            }

            builder.Append(Relative(repositoryRoot, occurrence.Path))
                .Append(':')
                .Append(occurrence.StartLine.ToString(CultureInfo.InvariantCulture))
                .Append('-')
                .Append(occurrence.EndLine.ToString(CultureInfo.InvariantCulture));
        }

        var remaining = group.Occurrences.Length - 1 - named;
        if (remaining > 0) {
            builder.Append(" and ").Append(remaining.ToString(CultureInfo.InvariantCulture)).Append(" more");
        }

        return builder.ToString();
    }

    static SkalaSeverity Severity(RuleSeverity severity) =>
        severity switch {
            RuleSeverity.Error => SkalaSeverity.Error,
            RuleSeverity.Warning => SkalaSeverity.Warning,
            RuleSeverity.Suggestion => SkalaSeverity.Info,
            _ => SkalaSeverity.Hidden
        };

    static string Relative(string root, string path) =>
        root.Length > 0 && Path.IsPathRooted(path) && path.StartsWith(root, StringComparison.Ordinal)
            ? Path.GetRelativePath(root, path).Replace('\\', '/')
            : path.Replace('\\', '/');

    static int TotalLines(LexedFile[] files) {
        var total = 0;
        foreach (var file in files) {
            total += file.LineCount;
        }

        return total;
    }

    /// <summary>One universe — production or test — measured end to end.</summary>
    /// <remarks>
    ///     ⚠ Production and test files are never in the same universe, so a group can never straddle the
    ///     two and its bucket is never a judgement call.
    /// </remarks>
    static (ImmutableArray<CloneGroup> Groups, int DuplicatedLines) Analyse(
        LexedFile[] universe,
        int minTokens,
        bool collapseHashes,
        CancellationToken cancellation
    ) {
        if (universe.Length == 0) {
            return ([], 0);
        }

        // ⚠ Path order, ordinal. It is the tie-break behind every "first occurrence" in the output and
        // behind which of two overlapping candidates the greedy pass takes, so it may not be the
        // caller's incidental order. Files with no tokens stay out of the index but still count
        // towards the denominator.
        var files = Array.FindAll(universe, static file => file.Tokens.Count > 0);
        Array.Sort(files, static (left, right) => string.CompareOrdinal(left.Path, right.Path));

        var fileStart = new int[files.Length + 1];
        for (var f = 0; f < files.Length; f++) {
            fileStart[f + 1] = fileStart[f] + files[f].Tokens.Count;
        }

        var total = fileStart[files.Length];
        var codes = new ushort[total];
        for (var f = 0; f < files.Length; f++) {
            files[f].Tokens.Codes.CopyTo(codes, fileStart[f]);
        }

        var windows = 0;
        for (var f = 0; f < files.Length; f++) {
            var length = fileStart[f + 1] - fileStart[f];
            if (length >= minTokens) {
                windows += length - minTokens + 1;
            }
        }

        if (windows == 0) {
            return ([], 0);
        }

        var keys = new ulong[windows];
        var at = new int[windows];
        RollingHash(codes, fileStart, minTokens, collapseHashes, keys, at, cancellation);

        Array.Sort(keys, at);
        var candidates = Verify(codes, keys, at, minTokens, cancellation);

        var consumed = new bool[total];
        var groups = new List<CloneGroup>();
        foreach (var candidate in candidates) {
            cancellation.ThrowIfCancellationRequested();
            if (Extend(candidate, codes, consumed, files, fileStart, minTokens) is { } group) {
                groups.Add(group);
            }
        }

        groups.Sort(static (left, right) => {
                var byPath = string.CompareOrdinal(left.Occurrences[0].Path, right.Occurrences[0].Path);
                if (byPath != 0) {
                    return byPath;
                }

                var byOffset = left.Occurrences[0].Start.CompareTo(right.Occurrences[0].Start);
                return byOffset != 0 ? byOffset : right.TokenLength.CompareTo(left.TokenLength);
            }
        );

        var duplicated = 0;
        foreach (var file in universe) {
            duplicated += file.DuplicatedLineCount;
        }

        return ([.. groups], duplicated);
    }

    /// <summary>
    ///     Step 2 — one hash per window of <c>minTokens</c>, rolled rather than recomputed.
    /// </summary>
    /// <remarks>
    ///     ⚠ Windows never span two files: the roll restarts at each file boundary. A window straddling
    ///     the join would be a clone of nothing.
    ///     <para>
    ///         ⚠ Token classes are mixed before they enter the polynomial. Raw <c>SyntaxKind</c> values are
    ///         small and clustered, and a polynomial over them modulo 2⁶⁴ leaves the low bits determined by
    ///         the last few tokens alone — which costs bucket quality, never correctness, but costs it over
    ///         the whole corpus.
    ///     </para>
    /// </remarks>
    static void RollingHash(
        ushort[] codes,
        int[] fileStart,
        int minTokens,
        bool collapseHashes,
        ulong[] keys,
        int[] at,
        CancellationToken cancellation
    ) {
        var power = 1UL;
        unchecked {
            for (var i = 1; i < minTokens; i++) {
                power *= Base;
            }
        }

        var w = 0;
        for (var f = 0; f + 1 < fileStart.Length; f++) {
            cancellation.ThrowIfCancellationRequested();
            var start = fileStart[f];
            var end = fileStart[f + 1];
            if (end - start < minTokens) {
                continue;
            }

            unchecked {
                var hash = 0UL;
                for (var i = start; i < start + minTokens; i++) {
                    hash = hash * Base + Mix(codes[i]);
                }

                keys[w] = collapseHashes ? 0UL : hash;
                at[w] = start;
                w++;

                for (var p = start + 1; p + minTokens <= end; p++) {
                    hash = (hash - Mix(codes[p - 1]) * power) * Base + Mix(codes[p + minTokens - 1]);
                    keys[w] = collapseHashes ? 0UL : hash;
                    at[w] = p;
                    w++;
                }
            }
        }
    }

    /// <summary>splitmix64's finaliser. Three multiplies, and it is why the buckets are the size they are.</summary>
    static ulong Mix(ushort code) {
        unchecked {
            var z = code + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    ///     Step 3's first half — turn hash buckets into <b>exactly verified</b> classes.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the guarantee. A bucket holds every window with one hash value, colliding ones
    ///     included; it is split into classes of windows that are equal token for token, and only a class
    ///     with two or more members survives. A collision leaves two singletons and produces nothing.
    ///     <para>
    ///         Splitting is a sort by content, so a pathologically large bucket costs <c>k log k</c>
    ///         comparisons and not <c>k²</c>.
    ///     </para>
    /// </remarks>
    static List<int[]> Verify(
        ushort[] codes,
        ulong[] keys,
        int[] at,
        int minTokens,
        CancellationToken cancellation
    ) {
        var byContent = new Comparison<int>((left, right) => Compare(codes, left, right, minTokens));
        var classes = new List<int[]>();

        var i = 0;
        while (i < keys.Length) {
            var j = i + 1;
            while (j < keys.Length && keys[j] == keys[i]) {
                j++;
            }

            if (j - i >= 2) {
                cancellation.ThrowIfCancellationRequested();
                var bucket = new int[j - i];
                Array.Copy(at, i, bucket, 0, j - i);
                Array.Sort(bucket, byContent);

                var k = 0;
                while (k < bucket.Length) {
                    var l = k + 1;
                    while (l < bucket.Length && Compare(codes, bucket[k], bucket[l], minTokens) == 0) {
                        l++;
                    }

                    if (l - k >= 2) {
                        var members = bucket[k..l];
                        Array.Sort(members);
                        classes.Add(members);
                    }

                    k = l;
                }
            }

            i = j;
        }

        // ⚠ By first token position, which is unique across classes because a window belongs to
        // exactly one. Earliest first, so the greedy pass below prefers the clone that appears first
        // in the tree rather than the one whose hash happened to sort first.
        classes.Sort(static (left, right) => left[0].CompareTo(right[0]));
        return classes;
    }

    static int Compare(ushort[] codes, int left, int right, int length) {
        for (var i = 0; i < length; i++) {
            var difference = codes[left + i].CompareTo(codes[right + i]);
            if (difference != 0) {
                return difference;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Step 3's second half and step 4 — extend one verified class to its maximal length and emit it
    ///     as one group.
    /// </summary>
    /// <remarks>
    ///     ⚠ <paramref name="consumed" /> is what makes "report each maximal clone group once" true, and
    ///     what it spends is <b>every window overlapping a reported occurrence</b>, not only the windows
    ///     that start inside one. A 250-token match contains 151 verified classes that all extend to the
    ///     same 250 tokens, and spending the interior collapses those into one finding — which is also
    ///     what keeps the pass linear instead of quadratic in the length of the duplicated region. The
    ///     windows straddling an occurrence's edge matter just as much: where A, B and C share 120 tokens
    ///     and B and C happen to share the token before them as well, the window one to the left is a
    ///     second verified class, and reporting it turns one duplication into a 120-token group of three
    ///     plus a 121-token group of two saying the same thing. A window stopping one token short of an
    ///     occurrence, or starting one token past its end, is untouched — that is a different clone.
    ///     <para>
    ///         The price is the standard greedy trade: a clone group that genuinely overlaps an
    ///         already-reported one is folded into it rather than reported beside it.
    ///     </para>
    ///     <para>
    ///         ⚠ Occurrences of one group may not overlap each other. A run of identical tokens
    ///         (<c>0, 0, 0, …</c>, a long initialiser) matches itself shifted by one, and without the gap cap
    ///         the group would be one region reported as a clone of itself.
    ///     </para>
    /// </remarks>
    static CloneGroup? Extend(
        int[] members,
        ushort[] codes,
        bool[] consumed,
        LexedFile[] files,
        int[] fileStart,
        int minTokens
    ) {
        var positions = new List<int>(members.Length);
        var owners = new List<int>(members.Length);
        foreach (var position in members) {
            if (consumed[position]) {
                continue;
            }

            var owner = FileOf(fileStart, position);
            if (positions.Count > 0 && owners[^1] == owner && position - positions[^1] < minTokens) {
                continue;
            }

            positions.Add(position);
            owners.Add(owner);
        }

        if (positions.Count < 2) {
            return null;
        }

        var maximum = int.MaxValue;
        for (var i = 1; i < positions.Count; i++) {
            if (owners[i] == owners[i - 1]) {
                maximum = Math.Min(maximum, positions[i] - positions[i - 1]);
            }
        }

        var left = 0;
        while (minTokens + left + 1 <= maximum && Agrees(codes, positions, owners, fileStart, -left - 1)) {
            left++;
        }

        var right = 0;
        while (minTokens + left + right + 1 <= maximum
               && Agrees(codes, positions, owners, fileStart, minTokens + right)) {
            right++;
        }

        var length = minTokens + left + right;

        // ⚠ Spent whether or not the group is reported. A declined table is still covered ground: leaving
        // its interior unconsumed would hand the same run back one token to the right, for every one of
        // its windows, and turn the greedy pass quadratic in the length of the list.
        for (var i = 0; i < positions.Count; i++) {
            var owner = owners[i];
            var from = Math.Max(fileStart[owner], positions[i] - left - minTokens + 1);
            var to = Math.Min(fileStart[owner + 1] - 1, positions[i] + minTokens + right - 1);
            for (var w = from; w <= to; w++) {
                consumed[w] = true;
            }
        }

        if (IsOneList(positions, owners, files, fileStart, left, length)) {
            return null;
        }

        var occurrences = ImmutableArray.CreateBuilder<CloneOccurrence>(positions.Count);
        for (var i = 0; i < positions.Count; i++) {
            var owner = owners[i];
            var file = files[owner];
            var firstToken = positions[i] - left - fileStart[owner];
            var lastToken = firstToken + length - 1;
            var start = file.Tokens.Starts[firstToken];
            var end = file.Tokens.Ends[lastToken];
            var startLine = file.Text.Lines.IndexOf(start);
            var endLine = file.Text.Lines.IndexOf(end - 1);
            file.MarkDuplicated(startLine, endLine);
            occurrences.Add(new CloneOccurrence(file.Path, start, end - start, startLine + 1, endLine + 1));
        }

        // Positions ascend and files are in path order, so the occurrences are already sorted by path
        // then offset and Occurrences[0] is the first occurrence the finding is reported at.
        return new(length, occurrences.ToImmutable());
    }

    /// <summary>
    ///     Whether every occurrence is a window over one uniform sibling run — <b>issue #333</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A list of similar rows matches itself, shifted, and no amount of extraction removes it.</b>
    ///     Identifiers normalise to one class, so a 290-element list of <c>new SomeAnalyzer(),</c> is
    ///     1 450 tokens with a period of five and its first hundred tokens are a verified clone of its
    ///     second hundred. This is the artefact <b>#323</b> removed for file headers, surviving wherever a
    ///     file holds a run of similar declarations — and it is why 13 of 26 <c>Formatting/</c> findings
    ///     were once triaged one at a time as "irreducible option tables" rather than fixed here.
    ///     <para>
    ///         ⚠ <b>The length test against the stride is what keeps real duplication reporting, and it is
    ///         not a threshold.</b> An occupance longer than the run's period provably spans more than one
    ///         element, so it is "these rows, then those rows" — nothing to extract. One that fits inside a
    ///         single element is a match of one element against another, which is a block pasted into a
    ///         list and is exactly the finding the rule is for. <see cref="TokenStream.Runs" /> records the
    ///         period rather than a token count so this question needs no number anybody has to choose;
    ///         raising <c>minTokens</c> instead would silence the 111- and 163-token pairs that are
    ///         genuine.
    ///     </para>
    ///     <para>
    ///         ⚠ Not restricted to one file, deliberately. Two option tables in two files are the same
    ///         artefact for the same reason as one table against itself: the rows carry no information the
    ///         normalisation has not already erased, so a cross-file match between them is evidence of
    ///         nothing. The narrower "same run in the same file" test would have left every one of those
    ///         findings standing.
    ///     </para>
    /// </remarks>
    static bool IsOneList(
        List<int> positions,
        List<int> owners,
        LexedFile[] files,
        int[] fileStart,
        int left,
        int length
    ) {
        for (var i = 0; i < positions.Count; i++) {
            var tokens = files[owners[i]].Tokens;
            var first = positions[i] - left - fileStart[owners[i]];
            var run = tokens.RunCovering(first, first + length);
            if (run < 0 || length <= tokens.StrideOf(run)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether every occurrence has the same token at <paramref name="offset" /> from its seed.</summary>
    static bool Agrees(ushort[] codes, List<int> positions, List<int> owners, int[] fileStart, int offset) {
        ushort expected = 0;
        for (var i = 0; i < positions.Count; i++) {
            var index = positions[i] + offset;
            if (index < fileStart[owners[i]] || index >= fileStart[owners[i] + 1]) {
                return false;
            }

            if (i == 0) {
                expected = codes[index];
            } else if (codes[index] != expected) {
                return false;
            }
        }

        return true;
    }

    static int FileOf(int[] fileStart, int position) {
        var found = Array.BinarySearch(fileStart, 0, fileStart.Length - 1, position);
        return found >= 0 ? found : ~found - 1;
    }

    /// <summary>One file, lexed, with the lines it turned out to duplicate.</summary>
    sealed class LexedFile {
        bool[]? duplicated;

        public required string Path { get; init; }

        public required bool IsTest { get; init; }

        public required SourceText Text { get; init; }

        public required TokenStream Tokens { get; init; }

        /// <summary>
        ///     <c>SourceText.Lines.Count</c>, less the header the lexer skipped.
        /// </summary>
        /// <remarks>
        ///     ⚠ The header leaves the denominator as well as the numerator, which is the call
        ///     <c>DuplicationPass</c> already makes for generated files: a line that can never be matched
        ///     must not dilute the ratio, or the percentage falls whenever somebody adds an import. See
        ///     <see cref="TokenStream.HeaderLines" />.
        ///     <para>
        ///         ⚠ This is <i>not</i> the <c>lineCount</c> <c>CheckCommand</c> reports. That one counts
        ///         every line of every tree and is a description of the repository; this one is the
        ///         denominator of one ratio and counts only what that ratio can measure.
        ///     </para>
        /// </remarks>
        public int LineCount => Text.Lines.Count - Tokens.HeaderLines;

        /// <summary>⚠ A set, not a sum: a line in three groups is one duplicated line.</summary>
        /// <remarks>
        ///     ⚠ Clamped to <see cref="LineCount" />. Skipping a header leaves a gap in the token stream,
        ///     and a clone group may span one exactly as it already spans a comment — so an occurrence can
        ///     mark a line the denominator no longer holds. Rare enough never to have been seen, and
        ///     <c>DuplicatedLines &lt;= TotalLines</c> is an invariant the tests assert.
        /// </remarks>
        public int DuplicatedLineCount {
            get {
                if (duplicated is null) {
                    return 0;
                }

                var count = 0;
                foreach (var line in duplicated) {
                    if (line) {
                        count++;
                    }
                }

                return Math.Min(count, LineCount);
            }
        }

        /// <summary>Marks the 0-based line range an occurrence touches, ends included.</summary>
        public void MarkDuplicated(int firstLine, int lastLine) {
            duplicated ??= new bool[Text.Lines.Count];
            for (var line = firstLine; line <= lastLine && line < duplicated.Length; line++) {
                duplicated[line] = true;
            }
        }
    }
}
