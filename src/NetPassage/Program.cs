using Microsoft.Azure.Relay;
using Microsoft.Extensions.Configuration;
using Microsoft.HybridConnections.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetPassage
{
    class Program
    {
        private static bool KeepRunning = true;
        private static IConfiguration AppConfig;
        private static IConfiguration UserConfig;
        private static string LeftSectionFiller;
        private static string MidSectionFiller;
        private static string ConnectionName;
        private static string TargetHttpRelay;
        private static bool IsHttpRelayMode;
        private static string ConnectionStatus = "offline";

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, eventArgs) => {
                // call methods to clean up
                eventArgs.Cancel = true;
                Program.KeepRunning = false;
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

            IsHttpRelayMode = UserConfig["Relay:Mode"].Equals("http", StringComparison.CurrentCultureIgnoreCase);
            
            var listenersCount = Int32.Parse(UserConfig["Relay:Connections"]);

            var configHeader = IsHttpRelayMode ? "Http" : "WebSocket";

            var relayNamespace = $"{UserConfig[$"{configHeader}:Namespace"]}.servicebus.windows.net";
            var keyName = UserConfig[$"{configHeader}:PolicyName"];
            var key = UserConfig[$"{configHeader}:PolicyKey"];

            ConnectionName = UserConfig[$"{configHeader}:ConnectionName"];
            TargetHttpRelay = UserConfig[$"{configHeader}:TargetServiceAddress"];

            // Define the list of awaitable parallel tasks for websocket listeners
            List<Task<bool>> activeListenerTasks = new List<Task<bool>>();
            CancellationTokenSource cts = new CancellationTokenSource();


            try
            {
                while (Program.KeepRunning)
                {
                    ShowAll();
                    // Do your work in here, in small chunks.
                    // If you literally just want to wait until ctrl-c,
                    // not doing anything, see the answer using set-reset events.
                    if (IsHttpRelayMode)
                    {
                        //Construct the Http hybrid proxy listeners tasks
                        for (int i = 0; i < listenersCount; i++)
                        {
                            activeListenerTasks.Add(RunHttpRelayAsync(new HttpListener(
                                relayNamespace,
                                ConnectionName,
                                keyName,
                                key,
                                TargetHttpRelay,
                                ConnectionEventHandler,
                                cts)));
                        }

                        // Wait for all the tasks to finish
                        Task.WaitAll(activeListenerTasks.ToArray());
                    }
                    else // WebSockets Relay Mode
                    {
                        // Create the WebSockets hybrid proxy listener
                        var webSocketListener = new WebSocketListener(
                            relayNamespace,
                            ConnectionName,
                            keyName,
                            key,
                            ProcessWebSocketMessagesHandler,
                            ConnectionEventHandler,
                            cts);

                        // Opening the listener establishes the control channel to
                        // the Azure Relay service. The control channel is continuously
                        // maintained, and is reestablished when connectivity is disrupted.
                        Program.KeepRunning = RunWebSocketRelayAsync(webSocketListener).GetAwaiter().GetResult();
                    }
                }
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
        /// Establishes the websocket connection with the Azure Relay service and then starts the listener
        /// Opening the listener establishes the control channel to
        /// the Azure Relay service. The control channel is continuously
        /// maintained, and is reestablished when connectivity is disrupted.
        /// </summary>
        /// <param name="httpRelayListener"></param>
        /// <returns></returns>
        static async Task<bool> RunHttpRelayAsync(HttpListener relayListener)
        {
            try
            {
                // Opens up the connection to the Relay service
                await relayListener.OpenAsync(ProcessHttpMessagesHandler);

                // Start a new thread that will continuously read the console.
                await relayListener.ListenAsync();
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                return false;
            }
            finally
            {
                // Close the connection
                await relayListener.CloseAsync();
            }

            return !relayListener.CTS.IsCancellationRequested;
        }

        /// <summary>
        /// RunWebsocketRelayAsync
        /// </summary>
        /// <param name="webSocketListener"></param>
        /// <returns></returns>
        static async Task<bool> RunWebSocketRelayAsync(WebSocketListener webSocketListener)
        {
            // Opening the listener establishes the control channel to
            // the Azure Relay service. The control channel is continuously
            // maintained, and is reestablished when connectivity is disrupted.
            await webSocketListener.OpenAsync();

            // Start a new thread that will continuously read the from the websocket and write to the target Http endpoint.
            await webSocketListener.ListenAsync(ProcessWebSocketMessagesHandler);

            // Close Websocket connection
            await webSocketListener.CloseAsync();

            // Return true, if the cancellation was requested, otherwise - false
            return webSocketListener.IsCancellationRequested();
        }

        /// <summary>
        /// Listener Response Handler
        /// </summary>
        /// <param name="context"></param>
        static async void ProcessHttpMessagesHandler(RelayedHttpListenerContext context)
        {
            var startTimeUtc = DateTime.UtcNow;

            try
            {
                // Send the request message to the target listener
                var requestMessage = await HttpListener.CreateHttpRequestMessageAsync(context, ConnectionName);
                var responseMessage = await SendHttpRequestAsync(requestMessage);

                // Send the response message back to the caller
                await HttpListener.SendResponseAsync(context, responseMessage);

                // Log the message out to the console
                Logger.LogRequest(requestMessage.Method.Method, requestMessage.RequestUri.LocalPath, $"\u001b[32m {responseMessage.StatusCode} \u001b[0m", $"Forwarded to {TargetHttpRelay}", ShowAll);
            }
            catch (RelayException re)
            {
                Logger.LogRequest("Http", ConnectionName, $"\u001b[31m {System.Net.HttpStatusCode.ServiceUnavailable} \u001b[0m", re.Message, ShowAll);
            }
            catch (Exception e)
            {
                Logger.LogRequest("Http", ConnectionName, $"\u001b[31m {e.GetType().Name} \u001b[0m", e.Message, ShowAll);
                HttpListener.SendErrorResponse(e, context);
            }
            finally
            {
                Logger.LogPerformanceMetrics(startTimeUtc);
                // The context MUST be closed here
                await context.Response.CloseAsync();
            }
        }


        /// <summary>
        /// The method initiates the connection.
        /// </summary>
        /// <param name="relayConnection"></param>
        static async void ProcessWebSocketMessagesHandler(RelayedHttpListenerContext context)
        {
            DateTime startTimeUtc = DateTime.UtcNow;
            long bytesSent = 0;

            try
            {
                // Send the request message to the target listener
                var requestMessage = await HttpListener.CreateHttpRequestMessageAsync(context, ConnectionName);
                var responseMessage = await SendHttpRequestAsync(requestMessage);

                // Send the response message back to the caller
                bytesSent = await HttpListener.SendResponseAsync(context, responseMessage);

                // Log the message out to the console
                Logger.LogRequest(requestMessage.Method.Method, requestMessage.RequestUri.LocalPath, $"Status: \u001b[32m{responseMessage.StatusCode}\u001b[0m   Sent: \u001b[32m{bytesSent}\u001b[0m bytes", $"Forwarded to {TargetHttpRelay}", ShowAll);

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
                Logger.LogRequest("Http", ConnectionName, $"\u001b[31m {System.Net.HttpStatusCode.ServiceUnavailable} \u001b[0m", re.Message, ShowAll);
            }
            catch (Exception e)
            {
                Logger.LogRequest("Http", ConnectionName, $"\u001b[31m {e.GetType().Name} \u001b[0m", e.Message, ShowAll);
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
        /// <returns></returns>
        static async Task<HttpResponseMessage> SendHttpRequestAsync(HttpRequestMessage requestMessage)
        {
            try
            {
                // Send the request message via Http
                using (var httpClient = new HttpClient { BaseAddress = new Uri(TargetHttpRelay, UriKind.RelativeOrAbsolute) })
                {
                    httpClient.DefaultRequestHeaders.ExpectContinue = true;
                    //httpClient.DefaultRequestHeaders.Add("Transfer-Encoding", "chunked");
                    return await httpClient.SendAsync(requestMessage);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw;
            }
        }

        static void ShowAll()
        {
            Console.Clear();
            ShowHeader(AppConfig);
            ShowConfiguration(UserConfig);
            ShowRequestsHeader();

            List<string> logs = new List<string>();
            logs.AddRange(Logger.Logs);
            logs.Reverse();

            foreach (var message in logs)
            {
                Console.WriteLine(message);
            }
        }

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

        static void ShowError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.WriteLine("");
            Console.ResetColor();
        }

        static void ShowConfiguration(IConfiguration config)
        {
            var relayNamespace = $"sb://{config[$"{config["Relay:Mode"]}:Namespace"]}.servicebus.windows.net";
            var IsHttpRelayMode = config["Relay:Mode"].Equals("http", StringComparison.CurrentCultureIgnoreCase);

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

            title = IsHttpRelayMode? "Http Forwarding" : "Websocket Forwarding";
            filler = string.Empty.PadRight(LeftSectionFiller.Length - title.Length > 0 ? LeftSectionFiller.Length - title.Length : 0);
            Console.WriteLine($"{title}{filler}{MidSectionFiller}{relayNamespace}/{ConnectionName} {(char)29} {TargetHttpRelay}");
        }

        /// <summary>
        /// Show Request Logs Header
        /// </summary>
        static void ShowRequestsHeader()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n\r\n\r");
            Console.WriteLine(IsHttpRelayMode ? "HTTP Requests" : "Websocket Requests");
            Console.WriteLine("___________________");
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
