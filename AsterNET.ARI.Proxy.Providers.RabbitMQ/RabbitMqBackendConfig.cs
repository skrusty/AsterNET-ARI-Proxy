namespace AsterNET.ARI.Proxy.Providers.RabbitMQ
{
	public class RabbitMqBackendConfig
	{
		public string AmqpUri { get; set; }
		public int Heartbeat { get; set; }
        public RabbitMqBackendQueueConfig DialogueQueueConfig { get; set; }
        public RabbitMqBackendQueueConfig ApplicationQueueConfig { get; set; }
        public bool CheckForClosedDialogues { get; set; }

		public static RabbitMqBackendConfig Create(dynamic config)
        {
            return new RabbitMqBackendConfig()
            {
                AmqpUri = config.AmqpUri,
                DialogueQueueConfig = CreateConfig(config.DialogueConfig),
                ApplicationQueueConfig = CreateConfig(config.AppQueueConfig),
                Heartbeat = config.Heartbeat,
                CheckForClosedDialogues = config.CheckForClosedDialogues
            };
        }

        private static RabbitMqBackendQueueConfig CreateConfig(dynamic config)
        {
            return new RabbitMqBackendQueueConfig()
            {
                AutoDelete = config.AutoDelete,
                Durable = config.Durable,
                Exclusive = config.Exclusive,
                TTL = config.TTL
            };
        }
    }

    public class RabbitMqBackendQueueConfig
    {
        public bool AutoDelete { get; set; }
        public bool Exclusive { get; set; }
        public bool Durable { get; set; }
        public int TTL { get; set; }
    }
}
