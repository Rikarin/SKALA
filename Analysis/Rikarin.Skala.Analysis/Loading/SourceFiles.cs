using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     How a loader opens a source file it was asked for, and what it records when it cannot.
/// </summary>
/// <remarks>
///     ⚠ One decision, taken by attempting the read, shared by all three loaders. #353 found the
///     loaders disagreeing on an unreadable file, and #356 found them disagreeing again once the first
///     was fixed: <c>loose</c> reported it and left it out of the count, <c>binlog</c> still let the
///     <c>UnauthorizedAccessException</c> escape and took the whole command down with no verdict, and
///     <c>workspace</c> let Roslyn substitute an empty document and analysed that in silence. Three
///     catch blocks with the same policy sentence over them drift apart; one method does not.
///     <para>
///         ⚠ The two failures are deliberately not the same case. A file that vanished between the
///         enumeration and the read raises <see cref="IOException" /> and is dropped without a word —
///         a file that no longer exists is not part of the tree, and failing an agent's <c>verify</c>
///         over it would report a finding about nothing. A file that <em>exists and may not be
///         read</em> raises <see cref="UnauthorizedAccessException" /> — which does not derive from
///         <see cref="IOException" /> — and is the one that has to be said out loud: <c>SK9015</c> on
///         SK9010's contract, reported against the file, left alone, counted, the run kept going and
///         the exit code non-zero. Skipping it quietly is the #345 defect, a file dropping out of the
///         report with nothing said.
///     </para>
/// </remarks>
internal static class SourceFiles {
    /// <summary>
    ///     Opens <paramref name="path" /> for reading, or returns null after recording why it could
    ///     not be.
    /// </summary>
    /// <remarks>
    ///     A denied read is added to <paramref name="unreadable" /> and reported once through
    ///     <paramref name="diagnostics" />; a vanished file is neither. Callers own the stream.
    /// </remarks>
    public static FileStream? Open(
        string path,
        ImmutableHashSet<string>.Builder unreadable,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        try {
            return File.OpenRead(path);
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException exception) {
            unreadable.Add(path);

            // ⚠ Once per file, not once per compilation. A multi-targeted project is one set of
            // source files opened as one compilation per moniker (#343), and both project loaders
            // share this diagnostics list across those compilations; the per-unit set above is what
            // each compilation counts, the line below is what the reader sees.
            if (!diagnostics.Any(known =>
                    known.Id == FormatDiagnosticIds.FileIoFailed
                    && string.Equals(known.File, path, StringComparison.Ordinal)
                )) {
                diagnostics.Add(
                    new SkalaDiagnostic(FormatDiagnosticIds.FileIoFailed, SkalaSeverity.Error, exception.Message, path)
                );
            }

            return null;
        }
    }

    /// <summary>Whether <paramref name="path" /> can be opened, recording it as above when it cannot.</summary>
    /// <remarks>
    ///     ⚠ For the loader that does not read the file itself. <c>MSBuildWorkspace</c> opens documents
    ///     through Roslyn's <c>FileTextLoader</c>, which turns a denied read into an <em>empty</em>
    ///     document and a workspace diagnostic nothing downstream reads — measured: <c>check
    ///     --load=workspace --no-formatting</c> over a two-file project with one mode-000 file printed
    ///     <c>OK  nothing to do.</c> at exit 0 with <c>fileCount: 2</c>. Asking the question ourselves,
    ///     the same way the other two loaders do, is what makes the three agree.
    /// </remarks>
    public static bool CanOpen(
        string path,
        ImmutableHashSet<string>.Builder unreadable,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        using var stream = Open(path, unreadable, diagnostics);
        return stream is not null;
    }
}
