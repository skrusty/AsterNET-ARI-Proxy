using System;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;
using AsterNET.ARI.Proxy.Providers.RabbitMQ;


namespace AsterNET.ARI.Proxy
{
	class Program
	{

		static void Main(string[] args)
		{
			// Init
			var provider = new RabbitMqProvider("amqp://", new RabbitMqOptions()
			{
				AutoDelete = false,
				Durable = true,
				Exclusive = false
			});

			var appProxy = new ApplicationProxy(provider, new StasisEndpoint("", 0, "", ""), "testapp");
			appProxy.Start();

			Console.CancelKeyPress += Console_CancelKeyPress;
			while (true)
				Console.ReadKey();
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			// Terminate Proxy		
				
		}
	}
}
