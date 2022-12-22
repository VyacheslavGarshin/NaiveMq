using NaiveMq.Client.Commands;
using NaiveMq.Service.Dto;

namespace NaiveMq.Service.Commands
{
    public class ServerStats : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }

        public bool Start { get; set; }

        public bool Finish { get; set; }

        public List<QueueStats> QueueStats { get; set; }

        public ServerStats()
        {
        }

        public ServerStats(string name, bool start, bool finish, List<QueueStats> queueStats = null)
        {
            Name = name;
            Start = start;
            Finish = finish;
            QueueStats = queueStats;
        }
    }
}