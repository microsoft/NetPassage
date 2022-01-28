using Microsoft.Azure.Relay;
using Microsoft.Extensions.Configuration;
using Microsoft.HybridConnections.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.HybridConnections.Relay
{
    class Program
    {
        public static HttpListener HttpRelayListener { get; set; }

        public static WebsocketClient WebsocketClient { get; set; }

        private static string ListenerNamespace;
        private static string ListenerConnectionName;
        private static string ListenerKeyName;
        private static string ListenerPolicyName;
        private static string ListenerTargetAddress;

        private static string RelayNamespace;
        private static string RelayConnectionName;
        private static string RelayKeyName;
        private static string RelayPolicyName;


        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables();
            var configuration = builder.Build();

            Logger.IsVerboseLogs = bool.Parse(configuration["Log:Verbose"]);

            ListenerNamespace = $"{configuration["Listener:Namespace"]}.servicebus.windows.net";
            ListenerConnectionName = configuration["Listener:ConnectionName"];
            ListenerKeyName = configuration["Listener:PolicyName"];
            ListenerPolicyName = configuration["Listener:PolicyKey"];
            ListenerTargetAddress = configuration["Listener:TargetServiceAddress"];

            RelayNamespace = $"{configuration["Relay:Namespace"]}.servicebus.windows.net";
            RelayConnectionName = configuration["Relay:ConnectionName"];
            RelayKeyName = configuration["Relay:PolicyName"];
            RelayPolicyName = configuration["Relay:PolicyKey"];

            var retryDelay = Int32.Parse(configuration["Relay:RetryFrequency"]);

            bool cancelConnection = false;

            do
            {
                cancelConnection = RunAsync().GetAwaiter().GetResult();
                Console.WriteLine($"Retrying to connect in {retryDelay} milliseconds...");
                Thread.Sleep(retryDelay); // sleep for configurable time (in millisec) and then re-try connection again
            } while (!cancelConnection);
        }

        static async Task<bool> RunAsync()
        {
            try
            {
                // Create the Http hybrid proxy listener
                // Create Http bi-directional connection with the Http bound target
                HttpRelayListener = new HttpListener(
                    ListenerNamespace,
                    ListenerConnectionName,
                    ListenerKeyName,
                    ListenerPolicyName,
                    "",
                    HttpEventHandler,
                    new CancellationTokenSource());

                // Opening the listener establishes the control channel to
                // the Azure Relay service. The control channel is continuously
                // maintained, and is reestablished when connectivity is disrupted.
                await HttpRelayListener.OpenAsync(ListenerRequestHandler);
                Console.WriteLine("Http Server listening");

                // Start a new thread that will continuously read messages over Http.
                await HttpRelayListener.ListenAsync();
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync(e.Message);
                throw;
            }

            return true;
        }



        /// <summary>
        /// Listener Response Handler
        /// </summary>
        /// <param name="context"></param>
        static async void ListenerRequestHandler(RelayedHttpListenerContext context)
        {
            var startTimeUtc = DateTime.UtcNow;

            try
            {
                // We'll use the Websocket client to send the content to a Websocket connection listener
                // Create the Websocket client
                WebsocketClient = new WebsocketClient(
                    RelayNamespace,
                    RelayConnectionName,
                    RelayKeyName,
                    RelayPolicyName);

                // Initiate the connection
                var relayConnection = await WebsocketClient.CreateConnectionAsync();
                if (!relayConnection)
                {
                    // There is no websocket listener that is actively listening to our connections, let's try again later
                    return;
                }

                // Listen to messages on the websocket connection
                var messageSent = await WebsocketClient.RelayAsync(context);

                await Logger.OutputRequestAsync(messageSent);

                // Close Websocket connection
                await WebsocketClient.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                HttpListener.SendErrorResponse(ex, context);
            }
            finally
            {
                Logger.LogPerformanceMetrics(startTimeUtc);
                // The context MUST be closed here
                await context.Response.CloseAsync();
            }
        }

        /// <summary>
        /// Outputs the Http listener's event messages
        /// </summary>
        /// <param name="eventMessage"></param>
        static void HttpEventHandler(string eventMessage)
        {
            Logger.LogRequest("Http", ListenerTargetAddress, $"\u001b[36m {System.Net.HttpStatusCode.OK} \u001b[0m", eventMessage);
            Console.WriteLine(eventMessage);
        }


    }
}
