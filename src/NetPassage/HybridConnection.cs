
namespace NetPassage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.Azure.Relay;
    using System.Net.Http;
    using System.Threading;
    using Microsoft.ServiceBusBotRelay.Core.Extensions;

    internal class HybridConnection
    {
        readonly HybridConnectionListener listener;
        readonly HttpClient httpClient;
        readonly string hybridConnectionSubpath;
        private CancellationTokenSource cancellationToken { get; set; }

        public HybridConnection(string relayNamespace, string connectionName, string keyName, string keyValue, Uri targetUri, Action<string> eventHandler, CancellationTokenSource cts)
        {
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, keyValue);
            this.listener = new HybridConnectionListener(new Uri($"sb://{relayNamespace}/{connectionName}"), tokenProvider);
            this.cancellationToken = cts;

            // Subscribe to the status events.
            this.listener.Connecting += (o, e) => { eventHandler("connecting"); };
            this.listener.Offline += (o, e) => { eventHandler("offline"); };
            this.listener.Online += (o, e) => { eventHandler("online"); };

            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = targetUri;
            this.httpClient.DefaultRequestHeaders.ExpectContinue = false;
            this.hybridConnectionSubpath = this.listener.Address.AbsolutePath.EnsureEndsWith("/");
        }

        public HybridConnection(string relayNamespace, string connectionName, string keyName, string keyValue, string targetScheme, string targetHost, Int32 targetPort, string targetQuery, Action<string> eventHandler, CancellationTokenSource cts)
        {
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, keyValue);
            this.listener = new HybridConnectionListener(new Uri($"sb://{relayNamespace}/{connectionName}"), tokenProvider);
            this.cancellationToken = cts;

            // Subscribe to the status events.
            this.listener.Connecting += (o, e) => { eventHandler("connecting"); };
            this.listener.Offline += (o, e) => { eventHandler("offline"); };
            this.listener.Online += (o, e) => { eventHandler("online"); };

            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = new UriBuilder(targetScheme, targetHost, targetPort, targetQuery).Uri;
            this.httpClient.DefaultRequestHeaders.ExpectContinue = false;
            this.hybridConnectionSubpath = this.listener.Address.AbsolutePath.EnsureEndsWith("/");
        }

        public HybridConnection(string connectionString, string targetUrl, Action<string> eventHandler, CancellationTokenSource cts)
        {
            var targetUri = new Uri(targetUrl);
            this.listener = new HybridConnectionListener(connectionString);
            this.cancellationToken = cts;

            // Subscribe to the status events.
            this.listener.Connecting += (o, e) => { eventHandler("connecting"); };
            this.listener.Offline += (o, e) => { eventHandler("offline"); };
            this.listener.Online += (o, e) => { eventHandler("online"); };

            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = new UriBuilder(targetUri.Scheme, targetUri.Host, targetUri.Port, targetUri.Query).Uri;
            this.httpClient.DefaultRequestHeaders.ExpectContinue = false;
            this.hybridConnectionSubpath = this.listener.Address.AbsolutePath.EnsureEndsWith("/");
        }


        public async Task OpenAsync(CancellationToken cancelToken)
        {
            this.listener.RequestHandler = (context) => this.RequestHandler(context);
            await this.listener.OpenAsync(cancelToken);
            Console.WriteLine($"Forwarding from {this.listener.Address} to {this.httpClient.BaseAddress}.");
            Console.WriteLine("utcTime, request, statusCode, bytesSent, durationMs");
        }

        public Task CloseAsync(CancellationToken cancelToken)
        {
            return this.listener.CloseAsync(cancelToken);
        }

        async void RequestHandler(RelayedHttpListenerContext context)
        {
            DateTime startTimeUtc = DateTime.UtcNow;
            long bytesSent = 0;
            try
            {
                HttpRequestMessage requestMessage = CreateHttpRequestMessage(context);
                HttpResponseMessage responseMessage = await this.httpClient.SendAsync(requestMessage);
                bytesSent = await SendResponseAsync(context, responseMessage);
                await context.Response.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.GetType().Name}: {e.Message}");
                SendErrorResponse(e, context);
            }
            finally
            {
                LogRequest(startTimeUtc, context, bytesSent);
            }
        }

        async Task<long> SendResponseAsync(RelayedHttpListenerContext context, HttpResponseMessage responseMessage)
        {
            context.Response.StatusCode = responseMessage.StatusCode;
            context.Response.StatusDescription = responseMessage.ReasonPhrase;
            foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
            {
                if (string.Equals(header.Key, "Transfer-Encoding"))
                {
                    continue;
                }

                context.Response.Headers.Add(header.Key, string.Join(",", header.Value));
            }
            context.Response.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");

            var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(context.Response.OutputStream);

            return responseStream.Length;
        }

        void SendErrorResponse(Exception e, RelayedHttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.InternalServerError;

#if DEBUG || INCLUDE_ERROR_DETAILS
            context.Response.StatusDescription = $"Internal Server Error: {e.GetType().FullName}: {e.Message}";
#endif
            context.Response.Close();
        }

        HttpRequestMessage CreateHttpRequestMessage(RelayedHttpListenerContext context)
        {
            var requestMessage = new HttpRequestMessage();
            if (context.Request.HasEntityBody)
            {
                requestMessage.Content = new StreamContent(context.Request.InputStream);
                string contentType = context.Request.Headers[HttpRequestHeader.ContentType];
                if (!string.IsNullOrEmpty(contentType))
                {
                    requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                }
            }

            string relativePath = context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);
            relativePath = relativePath.Replace(this.hybridConnectionSubpath, string.Empty, StringComparison.OrdinalIgnoreCase);
            requestMessage.RequestUri = new Uri(relativePath, UriKind.RelativeOrAbsolute);
            requestMessage.Method = new HttpMethod(context.Request.HttpMethod);

            foreach (var headerName in context.Request.Headers.AllKeys)
            {
                if (string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerName, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    // Don't flow these headers here
                    continue;
                }

                requestMessage.Headers.Add(headerName, context.Request.Headers[headerName]);
            }

            return requestMessage;
        }

        void LogRequest(DateTime startTimeUtc, RelayedHttpListenerContext context, long bytesSent)
        {
            DateTime stopTimeUtc = DateTime.UtcNow;
            StringBuilder buffer = new StringBuilder();
            buffer.Append($"{startTimeUtc.ToString("s", CultureInfo.InvariantCulture)}, ");
            buffer.Append($"\"{context.Request.HttpMethod} {context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped)}\", ");
            buffer.Append($"{(int)context.Response.StatusCode}, ");
            buffer.Append($"{(long)bytesSent}, ");
            buffer.Append($"{(int)stopTimeUtc.Subtract(startTimeUtc).TotalMilliseconds}");
            Console.WriteLine(buffer);
        }

    }
}
