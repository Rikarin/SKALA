using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Protocol;

namespace Rikarin.Skala.Server;

/// <summary>
/// The one place the CLI reaches for the daemon.
/// </summary>
/// <remarks>
/// ⚠ It serves exactly one shape — a single named file, no <c>--staged</c>, no <c>--range</c>, no
/// overrides — because that is the shape the budget is about. docs/plan/13 § "Budgets" puts the
/// 40 ms warm number on "format one 500-line file", which is the agent hook and the editor's
/// format-on-save; a whole-corpus run is already parallel and is bounded by the formatter rather
/// than by process start. Serving more shapes here would mean a second implementation of the
/// reporting, the writing and the exit codes, and docs/plan/11's rule is that the daemon may not
/// have behaviour the CLI does not.
/// <para>
/// ⚠ Every failure returns null and the caller falls through. A daemon is an optimisation, and an
/// optimisation that can fail a pre-commit hook is not one.
/// </para>
/// </remarks>
public static class DaemonUse {
    public static CommandResult? TryFormat(FormatRequest request) {
        if (!DaemonClient.Enabled
            || request.Staged != StagedMode.Off
            || request.Range is not null
            || request.Overrides.Count > 0
            || request.Paths.Count != 1
            || !File.Exists(request.Paths[0])) {
            return null;
        }

        var path = Path.GetFullPath(request.Paths[0]);
        var root = request.RepositoryRoot ?? FormatCommand.FindRepositoryRoot(path);
        if (root is null) {
            return null;
        }

        var response = DaemonClient.Send(
            root,
            new DaemonRequest { Command = "format", Path = path, Define = request.Define }
        );
        if (response is not { Ok: true, Formatted: not null }) {
            // ⚠ There is no `skala daemon start`, so this is the lazy start docs/plan/11 promises:
            // the first single-file format in a repository leaves a daemon behind for the second.
            // It does not wait for it and this run falls through to doing the work itself.
            DaemonClient.StartInBackground(root);
            return null;
        }

        var output = new StringBuilder();
        if (response.Changed) {
            if (request.Diff) {
                output.Append(
                    UnifiedDiff.Render(
                        Path.GetRelativePath(root, path).Replace('\\', '/'),
                        File.ReadAllText(path),
                        response.Formatted
                    )
                );
            } else if (!request.Quiet) {
                output.Append(Path.GetRelativePath(root, path).Replace('\\', '/')).AppendLine();
            }

            if (!request.Check && !request.Diff) {
                File.WriteAllText(path, response.Formatted, new UTF8Encoding(false));
            }
        }

        foreach (var diagnostic in response.Diagnostics) {
            output.AppendLine(diagnostic);
        }

        if (!request.Quiet) {
            output.Append(response.Changed ? "1 file " : "0 files ")
                .Append(request.Check || request.Diff ? "would be reformatted" : "reformatted")
                .Append(", ")
                .Append(response.Changed ? "0" : "1")
                .AppendLine(" left alone");
        }

        var exit = (request.Check || request.Diff) && response.Changed
            ? ExitCodes.FormattingNeeded
            : ExitCodes.Ok;
        return new CommandResult(exit, output.ToString());
    }
}
