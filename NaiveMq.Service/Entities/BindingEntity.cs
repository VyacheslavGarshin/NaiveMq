using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Entities
{
    public class BindingEntity
    {
        public string Exchange { get; set; }

        public string Queue { get; set; }

        public bool Durable { get; set; }

        public string Pattern { get; set; }

        public static BindingEntity FromCommand(AddBinding command)
        {
            return new BindingEntity
            {
                Exchange = command.Exchange,
                Queue = command.Queue,
                Durable = command.Durable,
                Pattern = command.Pattern
            };
        }
    }
}
