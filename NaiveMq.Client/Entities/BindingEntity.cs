namespace NaiveMq.Client.Entities
{
    public class BindingEntity : BindingKey
    {
        public bool Durable { get; set; }

        public string Pattern { get; set; }
    }
}
