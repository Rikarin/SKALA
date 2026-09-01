using System.Collections.Generic;

namespace Contoso.Design;

public sealed class Batch {
    internal readonly ICollection<string> Tags = new HashSet<string>();
}
