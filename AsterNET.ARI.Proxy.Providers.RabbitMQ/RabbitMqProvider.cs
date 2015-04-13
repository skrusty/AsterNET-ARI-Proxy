using System;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;

namespace AsterNET.ARI.Proxy.Providers.RabbitMQ
{
	public class RabbitMqProvider : IBackendProvider
	{
		private readonly string _amqpUri;
		private readonly RabbitMqOptions _options;

		public RabbitMqProvider(string amqpUri, RabbitMqOptions options)
		{
			_amqpUri = amqpUri;
			_options = options;
		}

		public IDialogue CreateDialogue()
		{
			throw new NotImplementedException();
		}
	}

	public class RabbitMqOptions
	{
		
	}

	public class RabbitMqDialogue : IDialogue
	{
		public event EventHandler<Command> OnNewCommandRequest;
		public event EventHandler OnDialogueDestroyed;

		public Guid DialogueId { get; set; }

		public void PushMessage(IDialogueMessage message)
		{
			throw new NotImplementedException();
		}
	}
}
