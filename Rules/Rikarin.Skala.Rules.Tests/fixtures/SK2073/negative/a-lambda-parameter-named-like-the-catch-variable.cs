// ⚠ #424: the inserted `ex` is a lookup at the call, and the lambda's own `ex` is nearer than the
// catch's. With an `int` item the rewritten call binds an `EventId` overload; with a `string` item
// it passes the item as the exception (#412's audit). Neither passes the caught exception.
using System;
using System.Collections.Generic;

namespace Serilog {
    public interface ILogger {
        void Error(string messageTemplate, params object[] propertyValues);
        void Error(Exception exception, string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    public sealed class Worker {
        public void Run(Serilog.ILogger log) {
            try {
                Console.WriteLine("work");
            } catch (Exception ex) {
                new List<string> { "s" }.ForEach(ex => log.Error("text {T} failed", ex));
            }
        }
    }
}
