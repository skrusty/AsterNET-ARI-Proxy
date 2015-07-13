using System;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Config;
using AsterNET.ARI.Proxy.Providers.RabbitMQ;
using Nancy.Hosting.Self;
using NLog;

namespace AsterNET.ARI.Proxy
{
	class Program
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		static void Main(string[] args)
		{
            Logger.Info("Starting Ari Proxy");

            // Load config
            ProxyConfig.Current = ProxyConfig.Load();

            // Init
            BackendProvider.Current = CreateProvider(ProxyConfig.Current.BackendProvider, ProxyConfig.Current.BackendConfig);
			

            // Load APCoRs if required
            if(ProxyConfig.Current.APCoR != null)
            {
                var host = new NancyHost(new Uri(ProxyConfig.Current.APCoR.BindUri));
                host.Start();
            }

			// Load Applicaton Proxies
			foreach (var app in ProxyConfig.Current.Applications)
			{
				
				var appProxy = ApplicationProxy.Create(BackendProvider.Current,
					new StasisEndpoint(ProxyConfig.Current.AriHostname, ProxyConfig.Current.AriPort, ProxyConfig.Current.AriUsername,
						ProxyConfig.Current.AriPassword), app.Trim());
			}


			Logger.Info("Load complete");
			// Wait for exit
			Console.CancelKeyPress += Console_CancelKeyPress;
			while (true)
				Console.ReadKey();
		}

		private static RabbitMqProvider CreateProvider(string providerId, dynamic config)
		{
			switch (providerId)
			{
				case "rmq":
					var rmqConfig = RabbitMqBackendConfig.Create(config);
					var provider = new RabbitMqProvider(rmqConfig);
					return provider;
				default:
					throw new Exception("Unknown Provider");
			}
			
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			// Terminate Proxy		
			ApplicationProxy.Instances.ForEach(x =>
			{
				x.Stop();
			});
		}
	}

    public class BackendProvider
    {
        public static IBackendProvider Current { get; set; }
    }
}
