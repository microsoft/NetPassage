using System;
using System.Collections.Generic;

namespace Microsoft.HybridConnections.Core
{
    [Serializable]
    public class RequestMessage
    {
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }
        public byte[] Content { get; set; }
        public string HttpMethod { get; set; }
        public string RemoteEndPoint { get; set; }
        public string Url { get; set; }
        public string HybridConnectionScheme { get; set; }
        public string HybridConnectionName { get; set; }
    }
}
