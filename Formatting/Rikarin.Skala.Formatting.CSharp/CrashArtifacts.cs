using Rikarin.Skala.Formatting.CSharp.Arrangement;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     Which safety layer refused, in the words the diagnostic gave the user.
/// </summary>
/// <remarks>
///     ⚠ <c>input.cs</c> plus <c>output.cs</c> shows <em>what</em> was proposed and never
///     <em>
///         who said
///         no
///     </em>. Three arrangement layers produce a byte-identical artefact shape — the re-bind threw, a
///     diagnostic appeared, an identifier changed meaning — and the first two even share a diagnostic id
///     (<c>SK9098</c>), so the folder alone cannot tell three different bugs apart. The message already
///     exists; before this it simply never reached the folder.
/// </remarks>
/// <param name="Layer">Which check refused; one of the constants below.</param>
/// <param name="DiagnosticId">The <c>SK…</c> id the user saw.</param>
/// <param name="Message">That diagnostic's message, verbatim.</param>
public sealed record CrashRefusal(string Layer, string DiagnosticId, string Message) {
    /// <summary>The formatter's unconditional token-stream promise (<c>SK9099</c>).</summary>
    public const string TokenStream = "format/token-stream";

    /// <summary>Layer 2 could not answer, because re-binding threw (<c>SK9098</c>).</summary>
    public const string RebindThrew = "arrange/rebind-threw";

    /// <summary>Layer 2: the rewritten document carries a diagnostic it did not have (<c>SK9098</c>).</summary>
    public const string DiagnosticDelta = "arrange/diagnostic-delta";

    /// <summary>Layer 3: a surviving identifier binds to something else (<c>SK9096</c>).</summary>
    public const string SymbolIdentity = "arrange/symbol-identity";

    /// <summary>
    ///     The arrangement settings that drove the rewrite, when a rewrite drove it.
    /// </summary>
    /// <remarks>
    ///     Null for a <c>format</c> refusal, where no arrangement rule ran and there is nothing to
    ///     record.
    /// </remarks>
    public ArrangementOptions? Arrangement { get; init; }
}

/// <summary>
///     Drops a ready-made regression test when the safety net fires.
/// </summary>
/// <remarks>
///     docs/plan/04 § "The safety net":
///     <c>.skala/crash/&lt;hash&gt;/{input.cs,output.cs,config.snapshot,refusal.txt}</c>. The point is
///     that the reproduction exists before anybody thinks to ask for it — a token-stream failure is rare,
///     is a Skala bug, and is nearly impossible to reconstruct from a log line.
/// </remarks>
public static class CrashArtifacts {
    public static string? Write(
        string? root,
        string path,
        string input,
        string output,
        in PhaseOneOptions options,
        CrashRefusal? refusal = null
    ) {
        if (root is null) {
            return null;
        }

        try {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..16];
            // ⚠ `root` is already the `.skala` directory. EnsureAt creates the crash folder and
            // leaves the self-ignore marker at `.skala/`, so a crash artefact never turns up as an
            // untracked file in the tree the user was formatting.
            var directory = Core.SkalaDirectory.EnsureAt(root, "crash", hash);
            File.WriteAllText(Path.Combine(directory, "input.cs"), input);
            File.WriteAllText(Path.Combine(directory, "output.cs"), output);
            File.WriteAllText(
                Path.Combine(directory, "config.snapshot"),
                Snapshot(path, options, refusal?.Arrangement)
            );
            WriteRefusal(directory, refusal);
            return directory;
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }
    }

    /// <summary>
    ///     Written, or removed — never left alone.
    /// </summary>
    /// <remarks>
    ///     ⚠ The folder is keyed on a hash of the <em>input alone</em>, so a second failure on the same
    ///     text lands in the same directory as the first. A <c>format</c> token-stream failure arriving
    ///     after an <c>arrange</c> refusal on that text would otherwise inherit the arrange run's
    ///     <c>refusal.txt</c> and name a layer that did not fire this time — a stale file that reads
    ///     exactly like a fresh one. <see cref="File.Delete" /> on a path that does not exist is a no-op,
    ///     so the null branch costs nothing on the common path.
    /// </remarks>
    static void WriteRefusal(string directory, CrashRefusal? refusal) {
        var file = Path.Combine(directory, "refusal.txt");
        if (refusal is null) {
            File.Delete(file);
            return;
        }

        var builder = new StringBuilder();
        builder.Append("# layer: ").AppendLine(refusal.Layer);
        builder.Append("# diagnostic: ").AppendLine(refusal.DiagnosticId);
        builder.AppendLine();
        builder.AppendLine(refusal.Message);
        File.WriteAllText(file, builder.ToString());
    }

    static string Snapshot(string path, in PhaseOneOptions options, ArrangementOptions? arrangement) {
        var builder = new StringBuilder();
        builder.Append("# source: ").AppendLine(path);
        builder.Append("# skala: ")
            .AppendLine(typeof(CrashArtifacts).Assembly.GetName().Version?.ToString() ?? "0.0.0");
        builder.AppendLine();
        builder.Append("indent_size = ").AppendLine(options.IndentSize.ToString(CultureInfo.InvariantCulture));
        builder.Append("max_line_length = ").AppendLine(options.MaxLineLength.ToString(CultureInfo.InvariantCulture));
        builder.Append("keep_blank_lines_in_code = ")
            .AppendLine(options.KeepBlankLinesInCode.ToString(CultureInfo.InvariantCulture));
        builder.Append("keep_blank_lines_in_declarations = ")
            .AppendLine(options.KeepBlankLinesInDeclarations.ToString(CultureInfo.InvariantCulture));
        builder.Append("new_line_before_open_brace = ").AppendLine(options.NewLineBeforeOpenBrace);

        if (arrangement is { } settings) {
            AppendArrangement(builder, settings);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Every <see cref="ArrangementOptions" /> value, for an artefact an arrangement produced.
    /// </summary>
    /// <remarks>
    ///     ⚠ Five phase-one keys are not a reproduction of an <c>arrange</c> failure even once they hold
    ///     real values. The rewrites are driven by <c>skala_null_checking_pattern_style</c>,
    ///     <c>skala_object_creation_when_type_evident</c>, the <c>skala_arguments_*</c> family and forty
    ///     more, none of which <see cref="PhaseOneOptions" /> carries at all.
    ///     <para>
    ///         ⚠ Reflection over the public instance properties, deliberately, rather than a hand-written
    ///         list of the fifty-odd names. A hand-written map is the shape that has already gone wrong
    ///         three times in this repository, and this one would go wrong <em>silently</em>: an
    ///         arrangement option added later would simply not appear in the artefact and no test would
    ///         notice, which is the same class of under-specification this method exists to end. The type
    ///         is a <c>typeof</c> literal, so trimming keeps the properties without an annotation.
    ///     </para>
    ///     <para>
    ///         The names are the struct's own, snake-cased; they are <b>not</b> .editorconfig keys and the
    ///         header says so, because several are derived rather than read — <c>EmptyStringIsLiteral</c>
    ///         and <c>OmitDefaultAccessibility</c> each collapse a key's value to a bool.
    ///     </para>
    ///     <para>
    ///         ⚠ Prefixed <c>arrange_</c> because two of them, <c>MaxLineLength</c> and
    ///         <c>IndentSize</c>, share a name with a phase-one key above and are
    ///         <em>
    ///             not the same
    ///             option
    ///         </em>. Measured on this repository: <see cref="PhaseOneOptions" /> reads
    ///         <c>skala_max_line_length</c>, which the root .editorconfig sets to 120, while
    ///         <see cref="ArrangementOptions" /> reads the inert generic <c>max_line_length</c>, which
    ///         nothing sets — so the two lines legitimately carry 120 and 1. Unprefixed the snapshot
    ///         would read as one key stated twice, contradicting itself, and a reader reconstructing the
    ///         run would take whichever came last.
    ///     </para>
    ///     <para>Ordered by name, so two artefacts for the same run diff empty.</para>
    /// </remarks>
    static void AppendArrangement(StringBuilder builder, in ArrangementOptions arrangement) {
        builder.AppendLine();
        builder.AppendLine("# arrangement (ArrangementOptions property names, not .editorconfig keys)");

        // ⚠ Boxed once. `GetValue` takes an object, and re-boxing per property would hand each read
        // a different copy — harmless for a readonly struct, wasteful, and confusing to read.
        var values = (object)arrangement;
        foreach (var property in typeof(ArrangementOptions)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(static property => property.GetIndexParameters().Length == 0
                         && property.PropertyType != typeof(PhaseOneOptions)
                     )
                     .OrderBy(static property => property.Name, StringComparer.Ordinal)) {
            builder.Append("arrange_")
                .Append(SnakeCase(property.Name))
                .Append(" = ")
                .AppendLine(Render(property.GetValue(values)));
        }
    }

    /// <summary>
    ///     ⚠ <c>true</c>/<c>false</c> rather than .NET's <c>True</c>/<c>False</c>, so the line is
    ///     the shape a reader would paste back into an .editorconfig.
    /// </summary>
    static string Render(object? value) =>
        value switch {
            null => string.Empty,
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

    static string SnakeCase(string name) {
        var builder = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++) {
            if (index > 0 && char.IsUpper(name[index])) {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[index]));
        }

        return builder.ToString();
    }
}
