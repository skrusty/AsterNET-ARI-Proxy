using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;
using Newtonsoft.Json;
using NLog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AsterNET.ARI.Proxy.Providers.RabbitMQ
{
	public class RabbitMqProvider : IBackendProvider
	{
		private readonly string _amqpUri;
		private readonly RabbitMqOptions _options;
		private readonly ConnectionFactory _rmqConnection;
		private readonly Dictionary<string, RabbitMqProducer> _controlChannels;

		public RabbitMqProvider(RabbitMqBackendConfig config)
		{
			_amqpUri = config.AmqpUri;
			_options = new RabbitMqOptions()
			{
				AutoDelete = config.AutoDelete,
				Durable = config.Durable,
				Exclusive = config.Exclusive
			};

			_rmqConnection = new ConnectionFactory {uri = new Uri(_amqpUri)};
			_controlChannels = new Dictionary<string, RabbitMqProducer>();
		}

		public IDialogue CreateDialogue(string appName)
		{
			if (!_controlChannels.ContainsKey(appName))
				CreateControlChannel(appName);

			// Assigned new Id
			var newDialogueId = Guid.NewGuid();

			// Create Dialoge Channels
			var rtn = new RabbitMqDialogue(
				new RabbitMqProducer(_rmqConnection.CreateConnection(), "events_" + newDialogueId, _options),
				new RabbitMqProducer(_rmqConnection.CreateConnection(), "responses_" + newDialogueId, _options),
				new RabbitMqConsumer(_rmqConnection.CreateConnection(), "commands_" + newDialogueId, _options),
				newDialogueId);

			// Send NewDialogue Event
			_controlChannels[appName].PushToQueue(JsonConvert.SerializeObject(new NewDialogInfo()
			{
				Application = appName,
				DialogId = newDialogueId.ToString(),
				ServerId = ""
			}));

			return rtn;
		}

		public void RegisterApplication(string appName)
		{
			CreateControlChannel(appName);
		}


		private void CreateControlChannel(string appName)
		{
			var newChannel = new RabbitMqProducer(_rmqConnection.CreateConnection(), appName, _options);
            _controlChannels.Add(appName, newChannel);
		}
	}

	public class RabbitMqOptions
	{
		public bool Durable { get; set; }
		public bool AutoDelete { get; set; }
		public bool Exclusive { get; set; }
	}

	public class RabbitMqDialogue : IDialogue
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly RabbitMqProducer _eventChannel;
		private readonly RabbitMqProducer _responseChannel;
		private readonly RabbitMqConsumer _requestChannel;

		public event EventHandler<Command> OnNewCommandRequest;
		public event EventHandler OnDialogueDestroyed;
		public Guid DialogueId { get; set; }

		public RabbitMqDialogue(RabbitMqProducer eventChannel, RabbitMqProducer responseChannel,
			RabbitMqConsumer requestChannel, Guid dialogueId)
		{
			_eventChannel = eventChannel;
			_responseChannel = responseChannel;
			_requestChannel = requestChannel;
			DialogueId = dialogueId;

			// Attach to Consumer Channel
			_requestChannel.ReadFromQueue(OnDequeue, OnError);
		}

		public void PushMessage(IDialogueMessage message)
		{
			if (message.Type == MessageType.Response)
			{
				// Push message onto response channel
				var request = message as DialogueResponseMessage;
				if (request == null) return;

				var reqJson = JsonConvert.SerializeObject(request.Body);
                Logger.Trace("Pushing response to dialogue {0}: {1}", DialogueId, reqJson);
                _responseChannel.PushToQueue(reqJson);
			}
			else if (message.Type == MessageType.Event)
			{
				// Push message to event queue
				var evt = message as DialogueEventMessage;
				if (evt == null) return;

				var evtJson = JsonConvert.SerializeObject(evt.Body);
                Logger.Trace("Pushing event to dialogue {0}: {1}", DialogueId, evtJson);
				_eventChannel.PushToQueue(evtJson);
			}
		}

		protected void OnDequeue(string message, RabbitMqConsumer sender, ulong deliveryTag)
		{
			Logger.Trace("Receiving request from dialogue {0}: {1}", DialogueId, message);
			if (OnNewCommandRequest == null) return;
			try
			{
				OnNewCommandRequest(this, JsonConvert.DeserializeObject<Command>(message));
			}
			catch (Exception ex)
			{
				Logger.Error("Error processing OnNewCommandRequest", ex);
			}
		}

		protected void OnError(Exception ex, RabbitMqConsumer sender, ulong deliveryTag)
		{
			Logger.Error("RabbitMq Consumer Error", ex);
		}
	}

	public class RabbitMqConsumer : IDisposable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly RabbitMqOptions _options;
		private EventingBasicConsumer _consumer;

		public RabbitMqConsumer(IConnection connection, string queueName, RabbitMqOptions options)
		{
			_options = options;
			Connection = connection;
			QueueName = queueName;

			CreateModel();
			Model.QueueDeclare(QueueName, options.Durable, options.Exclusive, options.AutoDelete, null);
		}

		/// <summary>
		///     Gets or sets the model.
		/// </summary>
		/// <value>The model.</value>
		private IModel Model { get; set; }

		/// <summary>
		///     Gets or sets the connection to rabbit
		/// </summary>
		/// <value>The connection to rabbit</value>
		public IConnection Connection { get; set; }

		/// <summary>
		///     Gets or sets the name of the queue.
		/// </summary>
		/// <value>The name of the queue.</value>
		public string QueueName { get; set; }

		public string DialogId { get; set; }

		public void Dispose()
		{
			Connection.Close();
		}

		/// <summary>
		///     Read a message from the queue.
		/// </summary>
		/// <param name="onDequeue">The action to take when receiving a message</param>
		/// <param name="onError">If an error occurs, provide an action to take.</param>
		public void ReadFromQueue(Action<string, RabbitMqConsumer, ulong> onDequeue,
			Action<Exception, RabbitMqConsumer, ulong> onError)
		{
			_consumer = new EventingBasicConsumer(Model);

			// Receive the message from the queue and act on that message
			_consumer.Received += (o, e) =>
			{
				try
				{
					var queuedMessage = Encoding.ASCII.GetString(e.Body);
					onDequeue.Invoke(queuedMessage, this, e.DeliveryTag);
					Model.BasicAck(e.DeliveryTag, false);
				}
				catch (Exception ex)
				{
					Logger.Error("Error reding from queue", ex);
				}
			};

			Model.BasicConsume(QueueName, false, _consumer);
		}

		public void StopReading()
		{
			Model.BasicCancel(_consumer.ConsumerTag);
		}

		public void Close()
		{
			Connection.Close();
		}

		private void CreateModel()
		{
			Model = Connection.CreateModel();
		}
	}

	/// <summary>
	/// </summary>
	public class RabbitMqProducer : IDisposable
	{
		public RabbitMqProducer(IConnection connection, string queueName, RabbitMqOptions options)
		{
			Connection = connection;
			QueueName = queueName;

			CreateModel();
			Model.QueueDeclare(QueueName, options.Durable, options.Exclusive, options.AutoDelete, null);
		}

		private IModel Model { get; set; }
		public IConnection Connection { get; set; }
		public string QueueName { get; set; }
		public string DialogId { get; set; }

		public void Dispose()
		{
			Connection.Close();
		}

		public void PushToQueue(string message)
		{
			var body = Encoding.UTF8.GetBytes(message);

			Model.BasicPublish("", QueueName, null, body);
		}

		public void Close()
		{
			Connection.Close();
		}

		private void CreateModel()
		{
			Model = Connection.CreateModel();
		}
	}
}