﻿using System;
using System.Collections.Generic;
using AsterNET.ARI.Proxy.Common;
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
			IBackendProvider provider = CreateProvider(ProxyConfig.Current.BackendProvider, ProxyConfig.Current.BackendConfig);
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
			_proxies.ForEach(x =>
			{
				x.Stop();
			});
		}
	}
}
