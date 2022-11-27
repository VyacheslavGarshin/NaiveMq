using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Entities;
using NaiveMq.Service.Handlers;

namespace NaiveMq.Service.Cogs
{
    public class Subscription : IDisposable
    {
        public bool _clientConfirm { get; set; }

        public TimeSpan? _clientConfirmTimeout { get; set; }

        private readonly ClientContext _context;

        private readonly Queue _queue;

        private bool _isStarted;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _sendTask;
        public Subscription(ClientContext context, Queue queue, bool clientConfirm, TimeSpan? clientConfirmTimeout)
        {
            _context = context;
            _queue = queue;
            _clientConfirm = clientConfirm;
            _clientConfirmTimeout = clientConfirmTimeout;
        }

        public void Start()
        {
            if (!_isStarted)
            {
                Stop();

                _cancellationTokenSource = new CancellationTokenSource();
                _sendTask = Task.Run(SendAsync, _cancellationTokenSource.Token);

                _isStarted = true;
            }
        }

        public void Stop()
        {
            if (_isStarted)
            {
                _cancellationTokenSource.Cancel();

                _isStarted = false;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task SendAsync()
        {
            var cancellationToken = _cancellationTokenSource.Token;

            while (_isStarted && !_context.CancellationToken.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await _queue.WaitDequeueAsync(cancellationToken);

                if (_queue.TryDequeue(out var messageEntity))
                {
                    if (_queue.Durable)
                    {
                        await _context.Storage.PersistentStorage.DeleteMessageAsync(_context.User.Username, _queue.Name, messageEntity.Id, cancellationToken);
                    }

                    try
                    {
                        var message = new Message { Confirm = _clientConfirm, ConfirmTimeout = _clientConfirmTimeout, Queue = messageEntity.Queue, Text = messageEntity.Text };

                        await _context.Client.SendAsync(message, cancellationToken);
                    }
                    catch
                    {
                        await new MessageHandler().ExecuteAsync(_context, new Message { Id = messageEntity.Id, Queue = messageEntity.Queue, Text = messageEntity.Text });
                    }
                }
            }
        }
    }
}