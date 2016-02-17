using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;
using Newtonsoft.Json;
using NLog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AsterNET.ARI.Proxy.Providers.RabbitMQ
{
	public class RabbitMqProvider : IBackendProvider, IDisposable
	{
	    private readonly RabbitMqBackendQueueConfig _appQueueOptions;
        private readonly RabbitMqBackendQueueConfig _dialogueQueueOptions;
        private readonly ConnectionFactory _rmqConnection;
		private readonly Dictionary<string, RabbitMqProducer> _controlChannels;
		private readonly List<RabbitMqDialogue> _activeDialogues;
		private readonly System.Threading.Timer _dialogueMonitor;

		public RabbitMqProvider(RabbitMqBackendConfig config)
		{
		    var amqpUri = config.AmqpUri;
		    _appQueueOptions = config.ApplicationQueueConfig;


		    _dialogueQueueOptions = config.DialogueQueueConfig;

            _rmqConnection = new ConnectionFactory
			{
				uri = new Uri(amqpUri),
				RequestedHeartbeat = (ushort)config.Heartbeat
			};
			_controlChannels = new Dictionary<string, RabbitMqProducer>();
			_activeDialogues = new List<RabbitMqDialogue>();

            if(config.CheckForClosedDialogues)
			    _dialogueMonitor = new System.Threading.Timer(CheckDialogues, null, 5000, 5000);
		}

		public IDialogue CreateDialogue(string appName)
		{
		    var newConn = _rmqConnection.CreateConnection();
            if (!_controlChannels.ContainsKey(appName))
				CreateControlChannel(appName, _appQueueOptions);

			// Assigned new Id
			var newDialogueId = Guid.NewGuid();

			// Create Dialoge Channels
			var rtn = new RabbitMqDialogue(
				new RabbitMqProducer(newConn, "events_" + newDialogueId, _dialogueQueueOptions),
				new RabbitMqProducer(newConn, "responses_" + newDialogueId, _dialogueQueueOptions),
				new RabbitMqConsumer(newConn, "commands_" + newDialogueId, _dialogueQueueOptions),
				newDialogueId, newConn);

			// Send NewDialogue Event
			_controlChannels[appName].PushToQueue(JsonConvert.SerializeObject(new NewDialogInfo()
			{
				Application = appName,
				DialogId = newDialogueId.ToString(),
				ServerId = ""
			}));

			lock (_activeDialogues)
			{
				_activeDialogues.Add(rtn);
			}

			return rtn;
		}

		public void RegisterApplication(string appName)
		{
			CreateControlChannel(appName, _appQueueOptions);
		}


		private void CreateControlChannel(string appName, RabbitMqBackendQueueConfig options)
		{
			var newChannel = new RabbitMqProducer(_rmqConnection.CreateConnection(), appName, options);
            _controlChannels.Add(appName, newChannel);
		}

		private void CheckDialogues(object sender)
		{
			lock (_activeDialogues)
			{
				_activeDialogues.RemoveAll(x => x.CheckState() == false);
			}
		}

	    public void Dispose()
	    {
            _dialogueMonitor.Change(Timeout.Infinite, Timeout.Infinite);
            foreach (var i in _controlChannels)
	        {
	            i.Value.Close();
            }
	        foreach (var i in _activeDialogues)
	        {
	            i.Close();
	        }
        }
	}

	public class RabbitMqDialogue : IDialogue
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly RabbitMqProducer _eventChannel;
		private readonly RabbitMqProducer _responseChannel;
		private readonly RabbitMqConsumer _requestChannel;
	    private readonly IConnection _dialogueConnection;

	    public event EventHandler<Command> OnNewCommandRequest;
		public event EventHandler OnDialogueDestroyed;
		public Guid DialogueId { get; set; }

	    public DateTime Created { get; set; }

	    public string PrimaryDialogueChannel { get; set; }

	    public bool AllowDelete { get; set; }

	    public RabbitMqDialogue(RabbitMqProducer eventChannel, RabbitMqProducer responseChannel,
			RabbitMqConsumer requestChannel, Guid dialogueId, IConnection dialogueConnection)
		{
			_eventChannel = eventChannel;
			_responseChannel = responseChannel;
			_requestChannel = requestChannel;
	        _dialogueConnection = dialogueConnection;
	        DialogueId = dialogueId;
	        Created = DateTime.Now;

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

	    public void Close()
	    {
            // Close the Dialogue
            // Stop reading
            _requestChannel.StopReading();
	        _requestChannel.Close();
	        _responseChannel.Close();
	        _eventChannel.Close();

            // Close Connection
	        _dialogueConnection.Close();

	        if (OnDialogueDestroyed != null)
                OnDialogueDestroyed(this, null);
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

		/// <summary>
		/// Checks the state of the open queues to ensure they've not been deleted
		/// </summary>
		/// <returns></returns>
		public bool CheckState()
		{
			if (_eventChannel.CheckState() && _responseChannel.CheckState() && _requestChannel.CheckState()) return true;

			if (OnDialogueDestroyed != null)
				OnDialogueDestroyed(this, null);

		    Close();

			return false;
		}
	}

	public class RabbitMqConsumer : IDisposable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly RabbitMqBackendQueueConfig _options;
		private EventingBasicConsumer _consumer;

		public RabbitMqConsumer(IConnection connection, string queueName, RabbitMqBackendQueueConfig options)
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
            Model.Close();
        }

		private void CreateModel()
		{
			Model = Connection.CreateModel();
		}

		public bool CheckState()
		{
			try
			{
				Model.QueueDeclarePassive(QueueName);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	/// <summary>
	/// </summary>
	public class RabbitMqProducer : IDisposable
	{
		public RabbitMqProducer(IConnection connection, string queueName, RabbitMqBackendQueueConfig options)
		{
			Connection = connection;
			QueueName = queueName;

			CreateModel();
		    var args = new Dictionary<string, object>();
            if(options.TTL > -1)
		        args["x-message-ttl"] = options.TTL;
			Model.QueueDeclare(QueueName, options.Durable, options.Exclusive, options.AutoDelete, args);
            
            // Handle Unroutable Messages
            Model.BasicReturn += Model_BasicReturn;
		}

        private void Model_BasicReturn(object sender, BasicReturnEventArgs e)
        {
            // Called when a message was pushed to the queue, but no consumers were availble to receive it
            // This only gets called if the message was pushed with the Mandatory Flag!

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
		    try
		    {
		        Model.Close();
		    }
		    catch (Exception ex)
		    {
		        // log out expcetion here
		    }
		}

		private void CreateModel()
		{
			Model = Connection.CreateModel();
		}

		public bool CheckState()
		{
			try
			{
				Model.QueueDeclarePassive(QueueName);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}