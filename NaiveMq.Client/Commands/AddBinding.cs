namespace NaiveMq.Client.Commands
{
    public class AddBinding : AbstractRequest<Confirmation>
    {
        public string Exchange { get; set; }

        public string Queue { get; set; }

        public bool Durable { get; set; }

        /// <summary>
        /// Reqex pattern.
        /// </summary>
        /// <remarks>If null or empty then any message will go to the queue.</remarks>
        public string Pattern { get; set; }
    }
}
