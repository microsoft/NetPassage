// ***********************************************************************
// Assembly         : NetPassage.exe
// Author           : Danny Garber
// Created          : 07-22-2021
//
// Last Modified By : dannygar
// Last Modified On : 02-04-2022
// ***********************************************************************
// <copyright file="Logger.cs" company="Microsoft">
//     Copyright ©  2022
// </copyright>
// <summary></summary>
// ***********************************************************************>

namespace Microsoft.HybridConnections.Core
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading.Tasks;

    public static class Logger
    {
        public static List<string> Logs { get; private set; } = new List<string>();
        public static bool IsVerboseLogs { get; set; }
        public static int MaxRows { get; set; }
        public static int LeftPad { get; set; }
        public static int MidPad { get; set; }

        /// <summary>
        /// Log Request message
        /// </summary>
        /// <param name="requestType"></param>
        /// <param name="requestAddress"></param>
        /// <param name="statusCode"></param>
        /// <param name="message"></param>
        public static void LogRequest(string requestType, string requestAddress, string statusCode, string message)
        {
            var leftSection = $"{DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)} {requestType}   {requestAddress}";
            var filler = string.Empty.PadRight((LeftPad - leftSection.Length > 0 ? LeftPad - leftSection.Length : 0) + MidPad);

            Logs.Add($"{leftSection}{filler}{statusCode}  {message}");

            if (Logs.Count >= MaxRows)
            {
                Logs.RemoveAt(0);
            }
        }


        /// <summary>
        /// Log Request message
        /// </summary>
        /// <param name="requestType"></param>
        /// <param name="requestAddress"></param>
        /// <param name="statusCode"></param>
        /// <param name="message"></param>
        /// <param name="logsHandler"></param>
        public static void LogRequest(string requestType, string requestAddress, string statusCode, string message, Action logsHandler)
        {
            LogRequest(requestType, requestAddress, statusCode, message);
            logsHandler();
        }


        /// <summary>
        /// Clear logs
        /// </summary>
        public static void ClearLogs()
        {
            Logs.Clear();
        }

        /// <summary>
        /// Logs the request activity
        /// </summary>
        /// <param name="requestMessage"></param>
        public static async Task<bool> LogRequestActivityAsync(HttpRequestMessage requestMessage)
        {
            if (requestMessage.Content == null || !IsVerboseLogs) return false;

            try
            {
                var content = await requestMessage.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Yellow;

                var formatted = content;
                if (IsValidJson(formatted))
                {
                    var s = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented
                    };

                    dynamic o = JsonConvert.DeserializeObject(content);
                    formatted = JsonConvert.SerializeObject(o, s);
                }

                Console.WriteLine(formatted);
                Console.ResetColor();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Logs the exception
        /// </summary>
        /// <param name="ex"></param>
        public static void LogException(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex);
            Console.WriteLine("");
            Console.ResetColor();
        }


        /// <summary>
        /// Validates the Json string
        /// </summary>
        /// <param name="strInput"></param>
        /// <returns></returns>
        private static bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            if ((!strInput.StartsWith("{") || !strInput.EndsWith("}")) && (!strInput.StartsWith("[") || !strInput.EndsWith("]")))
            {
                return false;
            }

            try
            {
                JToken.Parse(strInput);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// OutputRequestAsync
        /// </summary>
        /// <param name="messageSent"></param>
        /// <returns></returns>
        public static async Task OutputRequestAsync(string messageSent)
        {
            if (!IsVerboseLogs) return;

            var activityRequest = RelayedHttpListenerRequestSerializer.DeserializeRequest(messageSent);
            await LogRequestActivityAsync(activityRequest);
        }

        /// <summary>
        /// Logs the request's starting time
        /// </summary>
        /// <param name="startTimeUtc"></param>
        public static void LogPerformanceMetrics(DateTime startTimeUtc)
        {
            if (!IsVerboseLogs) return;

            var stopTimeUtc = DateTime.UtcNow;

            Logs.Add($"and back {stopTimeUtc.Subtract(startTimeUtc).TotalMilliseconds} ms...");

            if (Logs.Count >= MaxRows)
            {
                Logs.RemoveAt(0);
            }
        }
    }
}
