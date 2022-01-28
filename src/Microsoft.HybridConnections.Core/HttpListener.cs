using Microsoft.Azure.Relay;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.HybridConnections.Core
{
    public class HttpListener
    {
        private readonly HttpClient _httpClient = null;
        private readonly HybridConnectionListener _listener;

        public CancellationTokenSource CTS { get; set; }

        /// <summary>
        /// The constructor
        /// </summary>
        /// <param name="relayNamespace"></param>
        /// <param name="connectionName"></param>
        /// <param name="keyName"></param>
        /// <param name="key"></param>
        /// <param name="targetServiceAddress"></param>
        /// <param name="eventHandler"></param>
        /// <param name="cts"></param>
        public HttpListener(string relayNamespace, string connectionName, string keyName, string key, string targetServiceAddress, Action<string> eventHandler, CancellationTokenSource cts)
        {
            CTS = cts;

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key);
            _listener = new HybridConnectionListener(new Uri($"sb://{relayNamespace}/{connectionName}"), tokenProvider);

            // Subscribe to the status events.
            _listener.Connecting += (o, e) => { eventHandler("connecting"); };
            _listener.Offline += (o, e) => { eventHandler("offline"); };
            _listener.Online += (o, e) => { eventHandler("online"); };

            if (!string.IsNullOrEmpty(targetServiceAddress))
            {
                // Send the request message via Http
                _httpClient = new HttpClient { BaseAddress = new Uri(targetServiceAddress, UriKind.RelativeOrAbsolute) };
                _httpClient.DefaultRequestHeaders.ExpectContinue = false;
            }
        }


        /// <summary>
        /// Convert RelayedHttpListenerContext into HttpRequestMessage
        /// </summary>
        /// <param name="context"></param>
        /// <param name="connectionName"></param>
        /// <returns></returns>
        public static async Task<HttpRequestMessage> CreateHttpRequestMessageAsync(RelayedHttpListenerContext context, string connectionName)
        {
            var requestMessage = new HttpRequestMessage();
            if (context.Request.HasEntityBody)
            {
                requestMessage.Content = new StreamContent(context.Request.InputStream);
                var contentType = context.Request.Headers[HttpRequestHeader.ContentType];
                if (!string.IsNullOrEmpty(contentType))
                {
                    requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                }
            }

            var relativePath = context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);
            relativePath = relativePath.Replace($"/{connectionName}/", string.Empty, StringComparison.OrdinalIgnoreCase);
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

            await Logger.LogRequestActivityAsync(requestMessage);

            //var requestMessageSer = await RelayedHttpListenerRequestSerializer.SerializeAsync(requestMessage);
            //var deserializedRequestMessage = RelayedHttpListenerRequestSerializer.Deserialize(requestMessageSer);

            return requestMessage;
        }


        /// <summary>
        /// Sends the response to the server
        /// </summary>
        /// <param name="context"></param>
        /// <param name="responseMessage"></param>
        /// <returns></returns>
        public static async Task SendResponseAsync(RelayedHttpListenerContext context, HttpResponseMessage responseMessage)
        {
            context.Response.StatusCode = responseMessage.StatusCode;
            context.Response.StatusDescription = responseMessage.ReasonPhrase;
            foreach (var header in responseMessage.Headers)
            {
                if (string.Equals(header.Key, "Transfer-Encoding"))
                {
                    continue;
                }

                context.Response.Headers.Add(header.Key, string.Join(",", header.Value));
            }

            var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(context.Response.OutputStream);
        }


        /// <summary>
        /// Sends the error response
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="context"></param>
        public static void SendErrorResponse(Exception ex, RelayedHttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.InternalServerError;
            context.Response.StatusDescription = $"Http Listener: Internal Server Error: {ex.GetType().FullName}: {ex.Message}";
            context.Response.Close();
        }

        /// <summary>
        // Opening the listener establishes the control channel to
        // the Azure Relay service. The control channel is continuously
        // maintained, and is reestablished when connectivity is disrupted.
        /// </summary>
        /// <param name="relayHandler"></param>
        /// <returns></returns>
        public async Task OpenAsync(Action<RelayedHttpListenerContext> relayHandler)
        {
            _listener.RequestHandler = relayHandler;
            await _listener.OpenAsync(CTS.Token);

            // Provide callback for a cancellation token that will close the listener.
            CTS.Token.Register(() => _listener.CloseAsync(CancellationToken.None));
        }

        /// <summary>
        /// Starts listening to the messages
        /// </summary>
        /// <returns></returns>
        public async Task ListenAsync()
        {
            // Start a new thread that will continuously read the console.
            await Console.In.ReadLineAsync().ContinueWith((s) => { CTS.Cancel(); });

            // Close the listener
            await _listener.CloseAsync();
        }


        /// <summary>
        /// Closes the listener after you exit the processing loop
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public Task CloseAsync()
        {
            if (_httpClient != null ) _httpClient.Dispose();
            return _listener.CloseAsync(CTS.Token);
        }


        /// <summary>
        /// Creates and sends the Http Request message
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendHttpRequestMessageAsync(RelayedHttpListenerContext context, string connectionName)
        {
            var requestMessage = new HttpRequestMessage();
            if (context.Request.HasEntityBody)
            {
                requestMessage.Content = new StreamContent(context.Request.InputStream);
                var contentType = context.Request.Headers[HttpRequestHeader.ContentType];
                if (!string.IsNullOrEmpty(contentType))
                {
                    requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                }
            }

            var relativePath = context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);
            relativePath = relativePath.Replace($"/{connectionName}/", string.Empty, StringComparison.OrdinalIgnoreCase);
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

            await Logger.LogRequestActivityAsync(requestMessage);

            //var requestMessageSer = await RelayedHttpListenerRequestSerializer.SerializeAsync(requestMessage);
            //var deserializedRequestMessage = RelayedHttpListenerRequestSerializer.Deserialize(requestMessageSer);

            // Send the request message via Http
            return (_httpClient != null) ? await _httpClient.SendAsync(requestMessage) : null;
        }
    }
}
