
namespace Microsoft.HybridConnections.Core
{
    using Microsoft.Azure.Relay;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class WebSocketListener
    {
        private readonly HybridConnectionListener _listener;
        private CancellationTokenSource _cancellationTokenSource { get; set; }

        /// <summary>
        /// The constructor
        /// </summary>
        /// <param name="relayNamespace"></param>
        /// <param name="connectionName"></param>
        /// <param name="keyName"></param>
        /// <param name="keyValue"></param>
        /// <param name="requestHandler"></param>
        /// <param name="eventHandler"></param>
        /// <param name="cts"></param>
        public WebSocketListener(string relayNamespace, string connectionName, string keyName, string keyValue, 
            Action<RelayedHttpListenerContext> requestHandler, Action<string> eventHandler, CancellationTokenSource cts)
        {
            if (string.IsNullOrEmpty(relayNamespace))
            {
                throw new ArgumentException($"'{nameof(relayNamespace)}' cannot be null or empty.", nameof(relayNamespace));
            }

            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException($"'{nameof(connectionName)}' cannot be null or empty.", nameof(connectionName));
            }

            if (string.IsNullOrEmpty(keyName))
            {
                throw new ArgumentException($"'{nameof(keyName)}' cannot be null or empty.", nameof(keyName));
            }

            if (string.IsNullOrEmpty(keyValue))
            {
                throw new ArgumentException($"'{nameof(keyValue)}' cannot be null or empty.", nameof(keyValue));
            }

            if (requestHandler is null)
            {
                throw new ArgumentNullException(nameof(requestHandler));
            }

            if (eventHandler is null)
            {
                throw new ArgumentNullException(nameof(eventHandler));
            }

            if (cts is null)
            {
                throw new ArgumentNullException(nameof(cts));
            }

            _cancellationTokenSource = cts;

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, keyValue);
            this._listener = new HybridConnectionListener(new Uri($"sb://{relayNamespace}/{connectionName}"), tokenProvider);

            // Subscribe to the request handler - the main processing flow
            this._listener.RequestHandler = (context) => requestHandler(context);

            // Subscribe to the status events.
            this._listener.Connecting += (o, e) => { eventHandler("connecting"); };
            this._listener.Offline += (o, e) => { eventHandler("offline"); };
            this._listener.Online += (o, e) => { eventHandler("online"); };
           
        }

        /// <summary>
        // Opening the listener establishes the control channel to
        // the Azure Relay service. The control channel is continuously
        // maintained, and is reestablished when connectivity is disrupted.
        /// </summary>
        /// <returns></returns>
        public async Task OpenAsync()
        {
            await _listener.OpenAsync(_cancellationTokenSource.Token);

            // trigger cancellation when the user presses enter. Not awaited.
#pragma warning disable CS4014
            _cancellationTokenSource.Token.Register(() => _listener.CloseAsync(CancellationToken.None));
            // Task.Run(() => Console.In.ReadLineAsync().ContinueWith((s) => { _cancellationTokenSource.Cancel(); }));
#pragma warning restore CS4014
        }


        /// <summary>
        /// Listener is ready to accept connections after it creates an outbound WebSocket connection
        /// </summary>
        /// <param name="relayProcessHandler"></param>
        /// <returns></returns>
        public async Task ListenAsync(Action<RelayedHttpListenerContext> relayProcessHandler)
        {
            // Initiate the connection and process messages
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
                //Task.Run(() =>
                //{
                //    // Initiate the connection and process messages
                //    this._listener.RequestHandler = (context) => relayProcessHandler(context);
                //});
#pragma warning restore CS4014
            }

            // close the listener after we exit the processing loop
            await _listener.CloseAsync(_cancellationTokenSource.Token);
        }



        /// <summary>
        /// Closes the listener after you exit the processing loop
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public Task CloseAsync()
        {
            return _listener.CloseAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// Returns true if the connection cancellation has been requested
        /// </summary>
        /// <returns></returns>
        public bool IsCancellationRequested()
        {
            return _cancellationTokenSource.IsCancellationRequested;
        }
    }
}
