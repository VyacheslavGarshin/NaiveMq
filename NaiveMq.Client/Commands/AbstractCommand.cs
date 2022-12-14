using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractCommand : ICommand
    {
        public Guid Id { get; set; }

        public virtual Task Prepare(CancellationToken cancellationToken)
        {
            if (Id == Guid.Empty)
            {
                Id = Guid.NewGuid();
            }

            return Task.CompletedTask;
        }

        public virtual void Validate()
        {
            if (Id == Guid.Empty)
            {
                throw new ClientException(ErrorCode.EmptyCommandId);
            }
        }

        public virtual Task Restore(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
