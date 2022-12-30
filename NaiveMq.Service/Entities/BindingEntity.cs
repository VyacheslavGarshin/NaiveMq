using NaiveMq.Client.Commands;
using System.Runtime.Serialization;

namespace NaiveMq.Service.Entities
{
    [DataContract]
    public class BindingEntity
    {
        [DataMember]
        public string Exchange { get; set; }

        [DataMember]
        public string Queue { get; set; }

        [DataMember]
        public bool Durable { get; set; }

        [DataMember]
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
