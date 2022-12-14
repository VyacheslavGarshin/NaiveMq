using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client.Commands
{
    public interface ICommand
    {
        /// <summary>
        /// Id of the command. Will be set by client on send if empty.
        /// </summary>
        public Guid Id { get; set; }

        public Task Prepare(CancellationToken cancellationToken);

        public void Validate();

        public Task Restore(CancellationToken cancellationToken);
    }
}
