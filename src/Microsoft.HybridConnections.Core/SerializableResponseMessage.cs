
namespace Microsoft.HybridConnections.Core
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class SerializableResponseMessage
    {
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }
        public string Content { get; set; }
        public string ReasonPhrase { get; set; }
        public string StatusCode { get; set; }
        public string requestMessage { get; set; }
        public bool IsSuccessStatusCode { get; set; }
    }
}
