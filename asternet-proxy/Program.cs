using System;
using System.Collections.Generic;
using AsterNET.ARI.Proxy.Config;
using AsterNET.ARI.Proxy.Config.ConfigurationProviders;
using AsterNET.ARI.Proxy.Providers.RabbitMQ;

namespace AsterNET.ARI.Proxy
{
	class Program
	{
		private static List<ApplicationProxy> _proxies;
		static void Main(string[] args)
		{
			// Load config
			ProxyConfig.Current = new JsonConfigurationProvider().LoadConfiguration<ProxyConfig>("config");
			
			// Init
			var rmqConfig = RabbitMqBackendConfig.Create(ProxyConfig.Current.BackendConfig);
			var provider = new RabbitMqProvider(rmqConfig);
			_proxies = new List<ApplicationProxy>();

			// Load Applicaton Proxies
			var apps = ProxyConfig.Current.Applications.Split(',');
			foreach (var app in apps)
			{
				var appProxy = new ApplicationProxy(provider,
					new StasisEndpoint(ProxyConfig.Current.AriHostname, ProxyConfig.Current.AriPort, ProxyConfig.Current.AriUsername,
						ProxyConfig.Current.AriPassword), app);
				_proxies.Add(appProxy);

				// Start Proxy
				appProxy.Start();
			}

			// Wait for exit
			Console.CancelKeyPress += Console_CancelKeyPress;
			while (true)
				Console.ReadKey();
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			// Terminate Proxy		
			_proxies.ForEach(x =>
			{
				x.Stop();
			});
		}
	}
}
