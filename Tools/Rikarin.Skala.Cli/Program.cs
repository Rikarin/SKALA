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
var code = parse.Invoke();
return parse.Errors.Count > 0 ? ExitCodes.ConfigurationError : code;
