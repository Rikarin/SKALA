using System.CommandLine;
using Rikarin.Skala.Cli;

return SkalaCommandLine.Create().Parse(args).Invoke();
