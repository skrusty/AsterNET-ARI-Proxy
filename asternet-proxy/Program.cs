using System;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;


namespace AsterNET.ARI.Proxy
{
	class Program
	{

		static void Main(string[] args)
		{
			// Init
			var dummyProvider = new DummyProvider();

			var appProxy = new ApplicationProxy(dummyProvider, new StasisEndpoint("", 0, "", ""), "testapp");
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

	
	public class DummyProvider : IBackendProvider
	{

		public DummyProvider()
		{
			
		}

		/// <summary>
		/// Creates a new diaglogue and pushes a new dialogue event to the control channel
		/// </summary>
		/// <returns></returns>
		public IDialogue CreateDialogue()
		{
			// Create the new dialogue queues

			// return the new dialogue object
			return new DummyDialogue()
			{
				DialogueId = Guid.NewGuid()
			};
		}
	}

	public class DummyDialogue : IDialogue
	{
		public event EventHandler<Command> OnNewCommandRequest;
		public event EventHandler OnDialogueDestroyed;

		public Guid DialogueId { get; set; }

		public void PushMessage(IDialogueMessage message)
		{
			switch (message.Type)
			{
				case MessageType.Event:
					
					break;
				case MessageType.Request:
					break;
				case MessageType.Response:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			
		}
	}
}
