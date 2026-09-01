// `{{Path}}` is literal text and cannot collide with the one real hole.
//
// ⚠ The escaped text is written *twice* on purpose. With one occurrence a parser that ignored `{{`
// would produce the two distinct names `{Path` and `Path`, find no duplicate and leave this fixture
// green — so the fixture would have covered the shape and discriminated nothing. With two, the same
// broken parser produces `{Path` twice and this rule fires, which is what makes the fixture a test.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, string path) {
            logger.Information("{{Path}} is the syntax, {Path} is the value, {{Path}} again", path);
        }
    }
}
