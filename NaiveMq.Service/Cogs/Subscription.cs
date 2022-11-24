using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Entities;
using NaiveMq.Service.Handlers;

namespace NaiveMq.Service.Cogs
{
    public class Subscription : IDisposable
    {
        private readonly HandlerContext _context;

        private readonly Queue _queue;

        private bool _isStarted;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _sendTask;

        public Subscription(HandlerContext context, Queue queue)
        {
            _context = context;
            _queue = queue;
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

                MessageEntity messageEntity = null;

                try
                {
                    messageEntity = (await new DequeueHandler().ExecuteAsync(_context, new Dequeue { Queue = _queue.Name }))?.Message;
                }
                catch (ServerException ex)
                {
                    _context.Logger.LogWarning(ex, "Warning during getting message for subscription.");
                }
                catch (Exception ex)
                {
                    _context.Logger.LogError(ex, "Error during getting message for subscription.");
                }

                if (messageEntity != null)
                {
                    // todo set confirm to message depends on subscription

                    try
                    {
                        var message = new Message { Confirm = false, Queue = messageEntity.Queue, Text = messageEntity.Text };

                        await _context.Client.SendAsync(message, cancellationToken);
                    }
                    catch
                    {
                        await new EnqueueHandler().ExecuteAsync(_context, new Enqueue { Id = messageEntity.Id, Queue = messageEntity.Queue, Text = messageEntity.Text });
                    }
                }
            }
        }
    }
}