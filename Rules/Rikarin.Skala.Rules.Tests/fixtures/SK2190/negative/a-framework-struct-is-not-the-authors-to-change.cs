using System;
using System.Collections.Generic;

namespace Fixtures {
    sealed class Log {
        readonly Dictionary<DateTime, string> byMoment = new Dictionary<DateTime, string>();

        readonly Dictionary<Guid, string> byId = new Dictionary<Guid, string>();

        public int Size => byMoment.Count + byId.Count;
    }
}
