﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaiveMq.Client.Entities;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace NaiveMq.Service.PersistentStorage
{
    public class FilePersistentStorage : IPersistentStorage
    {
        private readonly IOptions<FilePersistentStorageOptions> _options;

        private readonly ILogger<FilePersistentStorage> _logger;

        private readonly string _basePath;

        public FilePersistentStorage(IOptions<FilePersistentStorageOptions> options, ILogger<FilePersistentStorage> logger)
        {
            _options = options;
            _logger = logger;

            _basePath = string.IsNullOrEmpty(options.Value.Path) ? AppDomain.CurrentDomain.BaseDirectory : options.Value.Path;
        }

        public async Task SaveUserAsync(UserEntity user, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetUserPath(user.Username), JsonConvert.SerializeObject(user), true, cancellationToken);
        }

        public async Task DeleteUserAsync(string user, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetUserPath(user), cancellationToken);
        }

        public Task<List<UserEntity>> LoadUsersAsync(CancellationToken cancellationToken)
        {
            var path = Path.GetDirectoryName(GetUserPath("tmp"));
            var result = LoadEntitiesList<UserEntity>(path);
            return Task.FromResult(result);
        }

        public async Task SaveQueueAsync(string user, QueueEntity queue, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetQueuePath(user, queue.Name), JsonConvert.SerializeObject(queue), true, cancellationToken);
        }

        public async Task DeleteQueueAsync(string user, string queue, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetQueuePath(user, queue), cancellationToken);
            await DeleteDirectoryAsync(Path.GetDirectoryName(GetMessagePath(user, queue, Guid.Empty)), cancellationToken);
        }

        public Task<List<QueueEntity>> LoadQueuesAsync(string user, CancellationToken cancellationToken)
        {
            var path = Path.GetDirectoryName(GetQueuePath(user, "tmp"));
            var result = LoadEntitiesList<QueueEntity>(path);
            return Task.FromResult(result);
        }

        public async Task SaveMessageAsync(string user, string queue, MessageEntity message, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetMessagePath(user, queue, message.Id), JsonConvert.SerializeObject(message), false, cancellationToken);
        }

        public async Task DeleteMessageAsync(string user, string queue, Guid enqueueId, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetMessagePath(user, queue, enqueueId), cancellationToken);
        }

        public Task<List<MessageEntity>> LoadMessagesAsync(string user, string queue, CancellationToken cancellationToken)
        {
            var result = new List<MessageEntity>();
            var path = Path.GetDirectoryName(GetMessagePath(user, queue, Guid.Empty));

            if (Directory.Exists(path))
            {
                foreach (var fileInfo in new DirectoryInfo(path).EnumerateFiles())
                {
                    var message = JsonConvert.DeserializeObject<MessageEntity>(File.ReadAllText(fileInfo.FullName, Encoding.UTF8));
                    result.Add(message);
                }
            }

            return Task.FromResult(result);
        }

        public async Task SaveBindingAsync(string user, BindingEntity binding, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetBindingPath(user, binding.Exchange, binding.Queue), JsonConvert.SerializeObject(binding), true, cancellationToken);
        }

        public async Task DeleteBindingAsync(string user, string exchange, string queue, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetBindingPath(user, exchange, queue), cancellationToken);
        }

        public Task<List<BindingEntity>> LoadBindingsAsync(string user, CancellationToken cancellationToken)
        {
            var path = Path.GetDirectoryName(GetBindingPath(user, "tmp", "tmp"));
            var result = LoadEntitiesList<BindingEntity>(path);
            return Task.FromResult(result);
        }

        private async Task DeleteFileAsync(string path, CancellationToken cancellationToken)
        {
            await DeleteAsync(path, () =>
            {
                // wait for file to appear. antivirus can meddle and subscription takes message earlier than it's written to disk.
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }, cancellationToken);
        }

        private async Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            await DeleteAsync(path, () =>
            {
                // wait for file to appear. antivirus can meddle and subscription takes message earlier than it's written to disk.
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }, cancellationToken);
        }

        private async Task DeleteAsync(string path, Action action, CancellationToken cancellationToken)
        {
            var sw = new Stopwatch();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    action();
                    break;
                }
                catch (Exception ex)
                {
                    if (sw.Elapsed > _options.Value.DeleteTimeout)
                    {
                        _logger.LogWarning(ex, $"FilePersistentStorage.DeleteMessageAsync message file deletion timeout '{path}'. Deletion skipped.");
                        return;
                    }
                }

                await Task.Delay(_options.Value.DeleteRetryInterval);
            }
        }

        private static List<T> LoadEntitiesList<T>(string path)
        {
            var result = new List<T>();

            if (Directory.Exists(path))
            {
                foreach (var fileInfo in new DirectoryInfo(path).EnumerateFiles())
                {
                    var queue = JsonConvert.DeserializeObject<T>(File.ReadAllText(fileInfo.FullName, Encoding.UTF8));
                    result.Add(queue);
                }
            }

            return result;
        }

        private static async Task WriteFileAsync(string path, string text, bool overwrite, CancellationToken cancellationToken)
        {
            if (overwrite || !File.Exists(path))
            {
                try
                {
                    await WriteAllTextAsync(path, text, cancellationToken);
                }
                catch (DirectoryNotFoundException)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    await WriteAllTextAsync(path, text, cancellationToken);
                }
            }
        }

        static async Task WriteAllTextAsync(string path, string text, CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(path, text, Encoding.UTF8, cancellationToken);
        }

        private string GetUserPath(string user)
        {
            return Path.Combine(_basePath, "users", $"{user}.json");
        }

        private string GetQueuePath(string user, string queue)
        {
            return Path.Combine(_basePath, "queues", user, $"{queue}.json");
        }

        private string GetBindingPath(string user, string exchange, string queue)
        {
            return Path.Combine(_basePath, "bindings", user, $"{exchange}-{queue}.json");
        }

        private string GetMessagePath(string user, string queue, Guid enqueueId)
        {
            return Path.Combine(_basePath, "data", user, queue, $"{enqueueId}.json");
        }

        public void Dispose()
        {
        }
    }
}