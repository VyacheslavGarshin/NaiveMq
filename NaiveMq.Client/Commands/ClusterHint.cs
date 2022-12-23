using NaiveMq.Client.Dto;
using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class ClusterHint : AbstractRequest<Confirmation>, IReplicable
    {
        public List<QueueHint> Hints { get; set; }

        public ClusterHint()
        {
        }

        public ClusterHint(List<QueueHint> hints)
        {
            Hints = hints;
        }

        public override void Validate()
        {
            base.Validate();

            if (Hints?.Count < 1)
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Hints) });
            }
        }
    }
}
