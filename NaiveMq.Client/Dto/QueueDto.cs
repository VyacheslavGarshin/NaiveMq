namespace NaiveMq.Client.Dto
{
    public class QueueDto
    {
        public string User { get; set; }

        public string Name { get; set; }

        public bool Durable { get; set; }

        public bool Exchange { get; set; }
    }
}
