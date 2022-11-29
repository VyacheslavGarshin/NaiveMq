namespace NaiveMq.Client.Entities
{
    public class BindingKey
    {
        public string Id
        {
            get
            {
                return $"{Exchange}-{Queue}";
            }

            set
            {
                var keys = value.Split("-");
                Exchange = keys[0];
                Queue = keys[1];
            }
        }

        public string Exchange { get; set; }

        public string Queue { get; set; }
    }
}
