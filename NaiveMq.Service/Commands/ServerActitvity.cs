using NaiveMq.Client.AbstractCommands;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Dto;
using System.Runtime.Serialization;

namespace NaiveMq.Service.Commands
{
    public class ServerActitvity : AbstractRequest<Confirmation>
    {
        [DataMember(Name = "N")]
        public string Name { get; set; }

        [DataMember(Name = "S")]
        public bool Start { get; set; }

        [DataMember(Name = "F")]
        public bool Finish { get; set; }

        [DataMember(Name = "AQ")]
        public List<ActiveQueue> ActiveQueues { get; set; }

        public ServerActitvity()
        {
        }

        public ServerActitvity(string name, bool start, bool finish, List<ActiveQueue> activeQueues = null)
        {
            Name = name;
            Start = start;
            Finish = finish;
            ActiveQueues = activeQueues;
        }
    }
}