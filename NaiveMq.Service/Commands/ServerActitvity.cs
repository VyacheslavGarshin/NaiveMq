using NaiveMq.Client.Commands;
using NaiveMq.Service.Dto;

namespace NaiveMq.Service.Commands
{
    public class ServerActitvity : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }

        public bool Start { get; set; }

        public bool Finish { get; set; }

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