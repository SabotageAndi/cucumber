using System;
using System.Collections;
using System.Collections.Generic;

namespace Gherkin
{
    public class SourceEvents : IEnumerable<SourceEvent>
    {
        List<string> paths;

        public SourceEvents (List<string> paths)
        {
            this.paths = paths;
        }

        public IEnumerator<SourceEvent> GetEnumerator ()
        {
            return new SourceEventEnumerator (paths);
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator();
        }
    }
}
