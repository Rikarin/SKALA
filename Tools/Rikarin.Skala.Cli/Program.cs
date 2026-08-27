using System.CommandLine;
using Rikarin.Skala.Cli;
using Rikarin.Skala.Core.Diagnostics;

// ⚠ A parse error is exit 3, not System.CommandLine's default 1. docs/plan/09 § "Exit codes" gives
// 1 to "gate failed" and 3 to "configuration error", and an unrecognized option is a configuration
// error — the invocation named something the tool does not have. A hook or a CI step that stops on
// 1 and reports "the gate failed" when the truth is "you spelled --verbose wrong" is reporting a
// finding that does not exist.
//
// `Invoke()` still runs, because System.CommandLine's own error rendering (the message, then the
// usage) is what the user needs to see; only the code it returns is overridden.
var parse = SkalaCommandLine.Create().Parse(args);

int code;
try {
    // ⚠ `EnableDefaultExceptionHandler = false` is the load-bearing half. System.CommandLine catches
    // every unhandled exception itself, prints "Unhandled exception:" and a stack trace, and returns
    // 1 — so a `try` around `Invoke()` alone never sees one, and SK-FUZZ-0001's crash reported "the
    // gate failed" from inside a handler that looked like it was doing the right thing. Turning the
    // library's handler off is what lets the catch below decide the code.
    code = parse.Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
} catch (OperationCanceledException) {
    return ExitCodes.Cancelled;
} catch (Exception exception) {
    // ⚠ The same argument as the parse-error line above, one class further out, and SK-FUZZ-0001 is
    // why it is here. An unhandled exception escaped every per-command handler and System
    // .CommandLine returned 0, so a crash on a 32-byte file reported success; before that it would
    // have reported 1, which doc 09 reserves for "the gate failed". Both are a wrong *success-shaped*
    // answer: in CI a crash is then indistinguishable from a finding, or from a clean run.
    //
    // 5 is "internal error", which is exactly what an unhandled exception is. The stack trace goes to
    // stderr rather than being swallowed, because the next unknown crash has to be diagnosable from
    // the CI log alone.
    Console.Error.WriteLine("skala: internal error — this is a Skala bug.");
    Console.Error.WriteLine(exception.ToString());
    return ExitCodes.InternalError;
}

return parse.Errors.Count > 0 ? ExitCodes.ConfigurationError : code;
