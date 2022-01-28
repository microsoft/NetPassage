using Microsoft.Azure.Relay;
using Microsoft.HybridConnections.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.HybridConnections.Relay
{
    public class WebsocketClient
    {
        private readonly HybridConnectionClient _client;
        private HybridConnectionStream _relayConnection;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="relayNamespace"></param>
        /// <param name="connectionName"></param>
        /// <param name="keyName"></param>
        /// <param name="key"></param>
        public WebsocketClient(string relayNamespace, string connectionName, string keyName, string key)
        {
            // Create a new hybrid connection client
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key);
            _client = new HybridConnectionClient(new Uri($"sb://{relayNamespace}/{connectionName}"), tokenProvider);
        }

        /// <summary>
        /// Initiate the websocket connection
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CreateConnectionAsync()
        {
            try
            {
                _relayConnection = await _client.CreateConnectionAsync();
                return true;
            }
            catch (EndpointNotFoundException)
            {
                Logger.LogRequest("WebSocket", _client.Address.LocalPath, "\u001b[30m Failed \u001b[0m", $"There are no listeners connected for this endpoint: {_client.Address.AbsoluteUri}.");
                return false;
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                return false;
            }
        }

        /// <summary>
        /// Send buffer to the websocket listener
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public async Task SendAsync(string buffer)
        {
            using (var writer = new StreamWriter(_relayConnection) { AutoFlush = true })
            {
                await writer.WriteAsync(buffer);
            }
        }

        /// <summary>
        /// Relay the RelayedHttpListenerContext to the Websocket Listener
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> RelayAsync(RelayedHttpListenerContext context)
        {
            try
            {
                var requestMessageSer = await RelayedHttpListenerRequestSerializer.SerializeAsync(context.Request);
                // Send to the websocket listener
                await SendAsync(requestMessageSer);

                return requestMessageSer;
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                throw;
            }
        }

        /// <summary>
        /// Close the websocket connection
        /// </summary>
        /// <returns></returns>
        public async Task CloseConnectionAsync()
        {
            await _relayConnection.CloseAsync(CancellationToken.None);
        }
    }
}
