namespace NaiveMq.Service.Entities
{
    public class BindingEntity
    {
        public string Exchange { get; set; }

        public string Queue { get; set; }

        public bool Durable { get; set; }

        public string Pattern { get; set; }
    }
}
