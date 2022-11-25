namespace NaiveMq.Client.Commands
{
    public class GetQueue : AbstractRequest<GetQueueResponse>
    {
        public string Name { get; set; }

        /// <summary>
        /// Try to get queue.
        /// </summary>
        /// <remarks>Return null if queue is not found. Overwise raise an exception. True by default.</remarks>
        public bool Try { get; set; } = true;
    }
}
