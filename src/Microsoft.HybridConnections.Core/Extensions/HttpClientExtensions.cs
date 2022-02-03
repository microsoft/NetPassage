
namespace Microsoft.HybridConnections.Core.Extensions
{
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public static class HttpClientExtensions
    {
        public static Task<Stream> ReadAsStreamAsync(this HttpContent content, bool isChunked)
        {
            if (!isChunked)
            {
                return content.ReadAsStreamAsync();
            }
            else
            {
                var task = content.ReadAsStreamAsync()
                .ContinueWith<Stream>((streamTask) =>
                {
                    var outputStream = new MemoryStream();
                    var buffer = new char[1024 * 1024];
                    var stream = streamTask.Result;

                    // No using() so that we don't dispose stream.
                    var tr = new StreamReader(stream);
                    var tw = new StreamWriter(outputStream);

                    while (!tr.EndOfStream)
                    {
                        var chunkSizeStr = tr.ReadLine().Trim();
                        var chunkSize = chunkSizeStr.Length + 1;
                        if (int.TryParse(chunkSizeStr, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int chunkSizeInBytes))
                        {
                            chunkSize = chunkSizeInBytes;
                        }

                        tr.ReadBlock(buffer, 0, chunkSize);
                        tw.Write(buffer, 0, chunkSize);
                        tr.ReadLine();
                    }
                    tw.Flush();

                    return outputStream;
                });

                return task;
            }


        }
    }
}
