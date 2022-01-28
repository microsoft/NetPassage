using Microsoft.Azure.Relay;
using Microsoft.ServiceBusBotRelay.Core.Extensions;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.HybridConnections.Core
{
    public class WebSocketListener
    {
        private readonly HybridConnectionListener _listener;

        public CancellationTokenSource CTS { get; set; }

        /// <summary>
        /// The constructor
        /// </summary>
        /// <param name="relayNamespace"></param>
        /// <param name="connectionName"></param>
        /// <param name="keyName"></param>
        /// <param name="key"></param>
        /// <param name="eventHandler"></param>
        /// <param name="cts"></param>
        public WebSocketListener(string relayNamespace, string connectionName, string keyName, string key, Action<string> eventHandler, CancellationTokenSource cts)
        {
            CTS = cts;

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key);
            _listener = new HybridConnectionListener(new Uri($"sb://{relayNamespace}/{connectionName}"), tokenProvider);

            // Subscribe to the status events.
            _listener.Connecting += (o, e) => { eventHandler("connecting"); };
            _listener.Offline += (o, e) => { eventHandler("offline"); };
            _listener.Online += (o, e) => { eventHandler("online"); };
        }

        /// <summary>
        // Opening the listener establishes the control channel to
        // the Azure Relay service. The control channel is continuously
        // maintained, and is reestablished when connectivity is disrupted.
        /// </summary>
        /// <returns></returns>
        public async Task OpenAsync()
        {
            await _listener.OpenAsync(CTS.Token);

            // trigger cancellation when the user presses enter. Not awaited.
#pragma warning disable CS4014
            CTS.Token.Register(() => _listener.CloseAsync(CancellationToken.None));
            Task.Run(() => Console.In.ReadLineAsync().ContinueWith((s) => { CTS.Cancel(); }));
#pragma warning restore CS4014
        }


        /// <summary>
        /// Listener is ready to accept connections after it creates an outbound WebSocket connection
        /// </summary>
        /// <param name="relayProcessHandler"></param>
        /// <returns></returns>
        public async Task ListenAsync(Action<HybridConnectionStream, CancellationTokenSource> relayProcessHandler)
        {
            while (true)
            {
                // Accept the next available, pending connection request.
                // Shutting down the listener will allow a clean exit with
                // this method returning null
                var relayConnection = await _listener.AcceptConnectionAsync();
                if (relayConnection == null)
                {
                    break;
                }

                // The following task processes a new session. We turn off the
                // warning here since we intentially don't 'await'
                // this call, but rather let the task handling the connection
                // run out on its own without holding for it
#pragma warning disable CS4014
                Task.Run(() =>
                {
                    // Initiate the connection and process messages
                    relayProcessHandler(relayConnection, CTS);
                });
#pragma warning restore CS4014
            }

            // close the listener after we exit the processing loop
            await _listener.CloseAsync(CTS.Token);
        }



        /// <summary>
        /// Closes the listener after you exit the processing loop
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public Task CloseAsync()
        {
            return _listener.CloseAsync(CTS.Token);
        }
    }
}
