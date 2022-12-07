using System.Text.RegularExpressions;

namespace NaiveMq.Service.Cogs
{
    public class Binding
    {
        public string Exchange { get; set; }

        public string Queue { get; set; }

        public bool Durable { get; set; }

        public Regex Pattern { get; set; }
    }
}
