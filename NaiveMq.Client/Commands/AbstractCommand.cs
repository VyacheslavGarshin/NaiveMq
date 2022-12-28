using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractCommand : ICommand
    {
        public Guid Id { get; set; }

        public virtual void Prepare()
        {
            if (Id == Guid.Empty)
            {
                Id = Guid.NewGuid();
            }
        }

        public virtual void Validate()
        {
            if (Id == Guid.Empty)
            {
                throw new ClientException(ErrorCode.EmptyCommandId);
            }
        }

        public virtual void Restore()
        {
        }
    }
}
