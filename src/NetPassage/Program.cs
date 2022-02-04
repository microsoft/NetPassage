// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="Program.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>


namespace NetPassage
{
    using Microsoft.Azure.Relay;
    using Microsoft.Extensions.Configuration;
    using Microsoft.HybridConnections.Core;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        private readonly static object ConsoleLock = new object();
        private static IConfiguration AppConfig;
        private static IConfiguration UserConfig;
        private static string LeftSectionFiller;
        private static string MidSectionFiller;
        private static string ConnectionStatus = "offline";
        private static string ConfigHeader = "Relay";
        private static string RelayNamespace;
        private static List<ConnectionSettings> ConnectionSettingsCollection;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, eventArgs) => {
                // call methods to clean up
                eventArgs.Cancel = true;
            };

            AppConfig = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            ShowHeader(AppConfig);

            // Set format
            Logger.MaxRows = int.Parse(AppConfig["App:MessageRows"]);
            Logger.LeftPad = int.Parse(AppConfig["App:LeftPad"]);
            Logger.MidPad = int.Parse(AppConfig["App:MidPad"]);
            Logger.IsVerboseLogs = bool.Parse(AppConfig["Log:Verbose"]);
            LeftSectionFiller = string.Empty.PadRight(Logger.LeftPad, ' ');
            MidSectionFiller = string.Empty.PadRight(Logger.MidPad, ' ');

            if (args.Length < 1)
            {
                ShowError("Missing configuration file commandline argument");
                Environment.Exit(0);
            }

            UserConfig = new ConfigurationBuilder()
                .AddJsonFile(args[0], false, true)
                .Build();


            // Get Relay information for all hybrid connections
            ConnectionSettingsCollection = UserConfig.GetSection($"{ConfigHeader}:ConnectionSettings")?.Get<List<ConnectionSettings>>();
            RelayNamespace = $"{UserConfig["Relay:Namespace"]}.servicebus.windows.net";

            // Define the list of awaitable parallel tasks for websocket listeners
            List<Task> activeListenerTasks = new List<Task>();
            CancellationTokenSource cts = new CancellationTokenSource();

            try
            {
                ShowAll();

                // Do your work in here, in small chunks.
                // If you literally just want to wait until ctrl-c,
                // not doing anything, see the answer using set-reset events.
                // Create the WebSockets hybrid proxy listeners for each connection
                foreach (var conn in ConnectionSettingsCollection)
                {
                    activeListenerTasks.Add(RunWebSocketRelayAsync(new WebSocketListener(
                    RelayNamespace,
                    conn,
                    ProcessWebSocketMessagesHandler,
                    ConnectionEventHandler,
                    cts), cts));
                }

                // Opening the listeners for each hybrid connection to control channel to
                // the Azure Relay service. The control channel is continuously
                // maintained, and is reestablished when connectivity is disrupted.
                // Wait for all the tasks to finish
                Task.WaitAll(activeListenerTasks.ToArray(), cts.Token);
            }
            catch (AggregateException ex)
            {
                var errors = new StringBuilder();
                errors.AppendLine("The following exceptions have been thrown: ");
                for (int j = 0; j < ex.InnerExceptions.Count; j++)
                {
                    errors.AppendLine($"\n-------------------------------------------------\n{ex.InnerExceptions[j]}");
                }
                ShowError(errors.ToString());
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        /// <summary>
        /// RunWebsocketRelayAsync
        /// </summary>
        /// <param name="webSocketListener"></param>
        /// <param name="cts"></param>
        /// <returns></returns>
        static async Task RunWebSocketRelayAsync(WebSocketListener webSocketListener, CancellationTokenSource cts)
        {
            try
            {
                // Opening the listener establishes the control channel to
                // the Azure Relay service. The control channel is continuously
                // maintained, and is reestablished when connectivity is disrupted.
                await webSocketListener.OpenAsync().ConfigureAwait(false);

                // Start a new thread that will continuously read the from the websocket and write to the target Http endpoint.
                await webSocketListener.ListenAsync().ConfigureAwait(false);

                // Close Websocket connection
                await webSocketListener.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                cts.Cancel();
                throw;
            }
        }


        /// <summary>
        /// The method initiates the connection.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="connectionSettings"></param>
        static async void ProcessWebSocketMessagesHandler(RelayedHttpListenerContext context, ConnectionSettings connectionSettings)
        {
            DateTime startTimeUtc = DateTime.UtcNow;
            long bytesSent = 0;

            try
            {
                // Send the request message to the target listener
                var requestMessage = await HttpListener.CreateHttpRequestMessageAsync(context, connectionSettings.HybridConnection, connectionSettings.TargetHttp);
                var responseMessage = await SendHttpRequestAsync(requestMessage, connectionSettings.TargetHttp);

                // Send the response message back to the caller
                bytesSent = await HttpListener.SendResponseAsync(context, responseMessage);

                // Log the message out to the console
                Logger.LogRequest(requestMessage.Method.Method, requestMessage.RequestUri.LocalPath, $"Status: \u001b[32m{responseMessage.StatusCode}\u001b[0m   Sent: \u001b[32m{bytesSent}\u001b[0m bytes", $"Forwarded to {connectionSettings.TargetHttp}", ShowAll);

                if (Logger.IsVerboseLogs)
                {
                    // Add verbose output to the console
                    var responseMessageSeri = await RelayedHttpListenerRequestSerializer.SerializeResponseAsync(responseMessage);
                    Logger.LogRequest(requestMessage.Method.Method, requestMessage.RequestUri.LocalPath, $"Response Message: ", responseMessageSeri, ShowAll);
                }

                // The context MUST be closed here
                await context.Response.CloseAsync();
            }
            catch (RelayException re)
            {
                Logger.LogRequest("Http", connectionSettings.HybridConnection, $"\u001b[31m {System.Net.HttpStatusCode.ServiceUnavailable} \u001b[0m", re.Message, ShowAll);
            }
            catch (Exception e)
            {
                Logger.LogRequest("Http", connectionSettings.HybridConnection, $"\u001b[31m {e.GetType().Name} \u001b[0m", e.Message, ShowAll);
                 HttpListener.SendErrorResponse(e, context);
            }
            finally
            {
                Logger.LogPerformanceMetrics(startTimeUtc);
            }
        }


        /// <summary>
        /// Creates and sends the Stream message over Http Relay connection
        /// </summary>
        /// <param name="requestMessage"></param>
        /// <param name="httpTarget"></param>
        /// <returns></returns>
        static async Task<HttpResponseMessage> SendHttpRequestAsync(HttpRequestMessage requestMessage, string httpTarget)
        {
            try
            {
                // Send the request message via Http
                using (var httpClient = new HttpClient { BaseAddress = new Uri(httpTarget, UriKind.RelativeOrAbsolute) })
                {
                    httpClient.DefaultRequestHeaders.ExpectContinue = true;
                    return await httpClient.SendAsync(requestMessage);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw;
            }
        }

        /// <summary>
        /// Show console output
        /// </summary>
        static void ShowAll()
        {
            lock (ConsoleLock)
            {
                Console.Clear();
                ShowHeader(AppConfig);
                ShowConfiguration();
                ShowRequestsHeader();

                List<string> logs = new List<string>();
                logs.AddRange(Logger.Logs);
                // logs.Reverse();

                foreach (var message in logs)
                {
                    Console.WriteLine(message);
                }
            }
        }

        /// <summary>
        /// Show the header
        /// </summary>
        /// <param name="config"></param>
        static void ShowHeader(IConfiguration config)
        {
            var appName = config["App:Name"];
            var author = config["App:Author"];
            var version = config["App:Version"];

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(appName);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" by ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(author);
            Console.WriteLine($"Version: {version}");
            Console.ResetColor();
            Console.WriteLine("\n\r\n\r");
        }

        /// <summary>
        /// Show the error output
        /// </summary>
        /// <param name="message"></param>
        static void ShowError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.WriteLine("");
            Console.ResetColor();
        }

        /// <summary>
        /// Display console header
        /// </summary>
        static void ShowConfiguration()
        {
            var relayNamespace = $"sb://{RelayNamespace}.servicebus.windows.net";

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{LeftSectionFiller}{MidSectionFiller}(Ctrl+C to quit)");
            Console.ForegroundColor = ConsoleColor.Green;
            var title = "Session Status";
            var filler = string.Empty.PadRight(LeftSectionFiller.Length - title.Length > 0 ? LeftSectionFiller.Length - title.Length : 0);
            Console.WriteLine($"{title}{filler}{MidSectionFiller}{ConnectionStatus}");

            Console.ForegroundColor = ConsoleColor.White;
            title = "Azure Relay Namespace";
            filler = string.Empty.PadRight(LeftSectionFiller.Length - title.Length > 0 ? LeftSectionFiller.Length - title.Length : 0);
            Console.WriteLine($"{title}{filler}{MidSectionFiller}{relayNamespace}");

            title = "Websocket to Http Forwarding";
            filler = string.Empty.PadRight(LeftSectionFiller.Length - title.Length > 0 ? LeftSectionFiller.Length - title.Length : 0);

            foreach (var settings in ConnectionSettingsCollection)
            {
                Console.WriteLine($"{title}{filler}{MidSectionFiller}{relayNamespace}/{settings.HybridConnection} {(char)29} {settings.TargetHttp}");
            }
        }

        /// <summary>
        /// Show Request Logs Header
        /// </summary>
        static void ShowRequestsHeader()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n\r\n\r");
            Console.WriteLine("Websocket Relay Requests");
            Console.WriteLine("__________________________");
            Console.WriteLine("\n\r");
        }


        /// <summary>
        /// Outputs the listener's event messages
        /// </summary>
        /// <param name="eventMessage"></param>
        static void ConnectionEventHandler(string eventMessage)
        {
            ConnectionStatus = eventMessage;
            ShowAll();
        }
    }
}
