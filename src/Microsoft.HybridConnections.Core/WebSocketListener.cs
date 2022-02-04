// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="WebSocketListener.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>


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
        /// <param name="connectionSettings"></param>
        /// <param name="requestHandler"></param>
        /// <param name="eventHandler"></param>
        /// <param name="cts"></param>
        public WebSocketListener(string relayNamespace, ConnectionSettings connectionSettings, 
            Action<RelayedHttpListenerContext, ConnectionSettings> requestHandler, Action<string> eventHandler, CancellationTokenSource cts)
        {
            if (string.IsNullOrEmpty(relayNamespace))
            {
                throw new ArgumentException($"'{nameof(relayNamespace)}' cannot be null or empty.", nameof(relayNamespace));
            }

            if (connectionSettings is null)
            {
                throw new ArgumentNullException(nameof(connectionSettings));
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

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionSettings.PolicyName, connectionSettings.PolicyKey);
            this._listener = new HybridConnectionListener(new Uri($"sb://{relayNamespace}/{connectionSettings.HybridConnection}"), tokenProvider);

            // Subscribe to the request handler - the main processing flow
            this._listener.RequestHandler = (context) => requestHandler(context, connectionSettings);

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
            await _listener.OpenAsync(_cancellationTokenSource.Token).ConfigureAwait(false);

            // trigger cancellation when the user presses enter. Not awaited.
#pragma warning disable CS4014
            _cancellationTokenSource.Token.Register(() => _listener.CloseAsync(CancellationToken.None));
            Task.Run(() => Console.In.ReadLineAsync().ContinueWith((s) => { _cancellationTokenSource.Cancel(); }));
#pragma warning restore CS4014
        }


        /// <summary>
        /// Listener is ready to accept connections after it creates an outbound WebSocket connection
        /// </summary>
        /// <param name="relayProcessHandler"></param>
        /// <returns></returns>
        public async Task ListenAsync()
        {
            // Initiate the connection and process messages
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                // Accept the next available, pending connection request.
                // Shutting down the listener will allow a clean exit with
                // this method returning null
                var relayConnection = await _listener.AcceptConnectionAsync();
                if (relayConnection == null)
                {
                    break;
                }
            }

            // close the listener after we exit the processing loop
            await _listener.CloseAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
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
