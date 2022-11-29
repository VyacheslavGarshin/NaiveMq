using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaiveMq.Client.Commands;
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
            await DeleteDirectoryAsync(GetUserQueuesPath(user), cancellationToken);
            await DeleteDirectoryAsync(GetUserBindingsPath(user), cancellationToken);
            await DeleteDirectoryAsync(GetUserMessagesPath(user), cancellationToken);
        }

        public async Task<UserEntity> LoadUserAsync(string user, CancellationToken cancellationToken)
        {
            return await LoadEntityAsync<UserEntity>(GetUserPath(user), cancellationToken);
        }

        public Task<IEnumerable<string>> LoadUserKeysAsync(CancellationToken cancellationToken)
        {
            var path = Path.GetDirectoryName(GetUserPath("tmp"));
            var result = LoadKeys(path);
            return Task.FromResult(result);
        }

        public async Task SaveQueueAsync(string user, QueueEntity queue, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetQueuePath(user, queue.Name), JsonConvert.SerializeObject(queue), true, cancellationToken);
        }

        public async Task DeleteQueueAsync(string user, string queue, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetQueuePath(user, queue), cancellationToken);
            await DeleteDirectoryAsync(GetQueueMessagesPath(user, queue), cancellationToken);
        }

        public async Task<QueueEntity> LoadQueueAsync(string user, string queue, CancellationToken cancellationToken)
        {
            return await LoadEntityAsync<QueueEntity>(GetQueuePath(user, queue), cancellationToken);
        }

        public Task<IEnumerable<string>> LoadQueueKeysAsync(string user, CancellationToken cancellationToken)
        {
            var result = LoadKeys(GetUserQueuesPath(user));
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

        public async Task<BindingEntity> LoadBindingAsync(string user, string exchange, string queue, CancellationToken cancellationToken)
        {
            return await LoadEntityAsync<BindingEntity>(GetBindingPath(user, exchange, queue), cancellationToken);
        }

        public Task<IEnumerable<BindingKey>> LoadBindingKeysAsync(string user, CancellationToken cancellationToken)
        {
            var result = LoadKeys(GetUserBindingsPath(user)).Select(x => new BindingKey { Id = x });
            return Task.FromResult(result);
        }

        public async Task SaveMessageAsync(string user, string queue, MessageEntity message, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetMessagePath(user, queue, message.Id), JsonConvert.SerializeObject(message), false, cancellationToken);
        }

        public async Task DeleteMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetMessagePath(user, queue, messageId), cancellationToken);
            await DeleteEmptyDirectoriesAsync(Path.GetDirectoryName(GetMessagePath(user, queue, messageId)), cancellationToken);
        }

        public async Task<MessageEntity> LoadMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken)
        {
            return await LoadEntityAsync<MessageEntity>(GetMessagePath(user, queue, messageId), cancellationToken);
        }

        public Task<IEnumerable<Guid>> LoadMessageKeysAsync(string user, string queue, CancellationToken cancellationToken)
        {
            var result = LoadKeys(GetQueueMessagesPath(user, queue)).Select(x => Guid.Parse(x));
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

        private async Task DeleteEmptyDirectoriesAsync(string path, CancellationToken cancellationToken)
        {
            var info = new DirectoryInfo(path);
            var parent = info.Parent;

            if (info.Exists && !info.EnumerateFiles().Any() && !info.EnumerateDirectories().Any())
            {
                info.Delete();
                await DeleteEmptyDirectoriesAsync(parent.FullName, cancellationToken);
            }
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

        private static IEnumerable<string> LoadKeys(string path)
        {
            if (Directory.Exists(path))
            {
                var result = Directory.EnumerateFiles(path).Select(x => Path.GetFileNameWithoutExtension(x));

                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    result = result.Concat(LoadKeys(Path.Combine(path, dir)));
                }

                return result;
            }

            return Enumerable.Empty<string>();
        }

        private static async Task<T> LoadEntityAsync<T>(string path, CancellationToken cancellationToken)
        {
            var text = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
            return JsonConvert.DeserializeObject<T>(text);
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
            return Path.Combine(_basePath, "users", $"{user.ToLowerInvariant()}.json");
        }

        private string GetUserQueuesPath(string user)
        {
            return Path.Combine(_basePath, "queues", user);
        }

        private string GetQueuePath(string user, string queue)
        {
            return Path.Combine(_basePath, "queues", user, $"{queue.ToLowerInvariant()}.json");
        }

        private string GetUserBindingsPath(string user)
        {
            return Path.Combine(_basePath, "bindings", user);
        }        

        private string GetBindingPath(string user, string exchange, string queue)
        {
            return Path.Combine(_basePath, "bindings", user, $"{exchange.ToLowerInvariant()}-{queue.ToLowerInvariant()}.json");
        }

        private string GetUserMessagesPath(string user)
        {
            return Path.Combine(_basePath, "data", user);
        }

        private string GetQueueMessagesPath(string user, string queue)
        {
            return Path.Combine(_basePath, "data", user, queue);
        }

        private string GetMessagePath(string user, string queue, Guid messageId)
        {
            var chunk = messageId.ToString();
            return Path.Combine(_basePath, "data", user, queue, chunk.Substring(0, 2), chunk.Substring(2, 2), $"{messageId}.json");
        }

        public void Dispose()
        {
        }
    }
}