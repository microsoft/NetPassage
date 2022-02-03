// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.Azure.Relay.ReverseProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Azure Relay Http Reverse Proxy\n(c) Microsoft Corporation\n");
            if (args == null || args.Length != 3)
            {
                Console.WriteLine("Requires three arguments: connection string, target uri and additional port.");
                Console.WriteLine("Example:");
                Console.WriteLine($"\tdotnet.exe {Assembly.GetEntryAssembly().ManifestModule.Name} Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=ListenKey;SharedAccessKey=XXXX;EntityPath=your_hc_name http:/host:3000/api/ 3001");
                return;
            }

            string connectionString = args[0];
            string targetUrl = args[1];
            var targetPort = Int32.Parse(args[2]);
            RunAsync(connectionString, targetUrl, targetPort, args).GetAwaiter().GetResult();
        }

        static async Task RunAsync(string connectionString, string targetUrl, Int32 targetPort, string[] args)
        {
            var hybridProxy1 = new HybridConnectionReverseProxy(connectionString, targetUrl);
            await hybridProxy1.OpenAsync(CancellationToken.None);

            //var targetUri = new Uri(targetUrl);
            //var hybridProxy2 = new HybridConnectionReverseProxy(connectionString, targetUri.Scheme, targetUri.Host, targetPort, targetUri.Query);
            //await hybridProxy2.OpenAsync(CancellationToken.None);

            Console.ReadLine();

            await hybridProxy1.CloseAsync(CancellationToken.None);
            //await hybridProxy2.CloseAsync(CancellationToken.None);
        }
    }
}
