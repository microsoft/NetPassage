using System.Collections.Generic;

namespace Microsoft.HybridConnections.Core.Extensions
{
    public static class WebHeaderCollectionExtensions
    {
        public static IEnumerable<KeyValuePair<string, string>> GetHeaders(this System.Net.WebHeaderCollection webHeaderCollection)
        {
            string[] keys = webHeaderCollection.AllKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                yield return new KeyValuePair<string, string>(keys[i], webHeaderCollection[keys[i]]);
            }
        }
    }
}
