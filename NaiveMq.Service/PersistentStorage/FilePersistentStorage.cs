using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaiveMq.Client.Enums;
using NaiveMq.Client.Serializers;
using NaiveMq.Service.Entities;
using Newtonsoft.Json;
using System.Text;

namespace NaiveMq.Service.PersistentStorage
{
    public class FilePersistentStorage : IPersistentStorage
    {
        private const string MessagesDirectory = "messages";
        
        private const string BindingsDirectory = "bindings";

        private const string QueuesDirectory = "queues";

        private const string UsersDirecotory = "users";

        private readonly FilePersistentStorageOptions _options;

        private readonly ILogger<FilePersistentStorage> _logger;

        private readonly string _basePath;
        
        private readonly string _baseClusterPath;

        private readonly NaiveCommandSerializer _messageSerializer = new();

        public FilePersistentStorage(IOptions<FilePersistentStorageOptions> options, ILogger<FilePersistentStorage> logger)
        {
            _options = options.Value;
            _logger = logger;

            _basePath = string.IsNullOrEmpty(_options.Path) ? AppDomain.CurrentDomain.BaseDirectory : _options.Path;
            _baseClusterPath = string.IsNullOrEmpty(_options.ClusterPath) ? _basePath : _options.ClusterPath;

            _logger.LogInformation("Base path '{BasePath}' for messages, cluster base path '{ClusterBasePath}' for other.", _basePath, _baseClusterPath);
        }

        public async Task SaveUserAsync(UserEntity user, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetUserPath(user.Username), JsonConvert.SerializeObject(user), true, cancellationToken);
        }

        public Task DeleteUserAsync(string user, CancellationToken cancellationToken)
        {
            DeleteFile(GetUserPath(user));
            DeleteDirectory(GetUserQueuesPath(user));
            DeleteDirectory(GetUserBindingsPath(user));
            DeleteDirectory(GetUserMessagesPath(user));
            return Task.CompletedTask;
        }

        public async Task<UserEntity> LoadUserAsync(string user, CancellationToken cancellationToken)
        {
            return await LoadEntityAsync<UserEntity>(GetUserPath(user), cancellationToken);
        }

        public Task<IEnumerable<string>> LoadUserKeysAsync(CancellationToken cancellationToken)
        {
            var result = LoadKeys(GetUsersPath());
            return Task.FromResult(result);
        }

        public async Task SaveQueueAsync(string user, QueueEntity queue, CancellationToken cancellationToken)
        {
            await WriteFileAsync(GetQueuePath(user, queue.Name), JsonConvert.SerializeObject(queue), true, cancellationToken);
        }

        public Task DeleteQueueAsync(string user, string queue, CancellationToken cancellationToken)
        {
            DeleteFile(GetQueuePath(user, queue));
            DeleteDirectory(GetQueueMessagesPath(user, queue));
            return Task.CompletedTask;
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

        public Task DeleteBindingAsync(string user, string exchange, string queue, CancellationToken cancellationToken)
        {
            DeleteFile(GetBindingPath(user, exchange, queue));
            return Task.CompletedTask;
        }

        public async Task<BindingEntity> LoadBindingAsync(string user, string binding, CancellationToken cancellationToken)
        {
            return await LoadEntityAsync<BindingEntity>(GetBindingPath(user, binding), cancellationToken);
        }

        public Task<IEnumerable<string>> LoadBindingKeysAsync(string user, CancellationToken cancellationToken)
        {
            var result = LoadKeys(GetUserBindingsPath(user));
            return Task.FromResult(result);
        }

        public async Task SaveMessageAsync(string user, string queue, MessageEntity message, CancellationToken cancellationToken)
        {
            var messageBytes = _messageSerializer.Serialize(message);
            var messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);

            await WriteFileAsync(
                GetMessagePath(user, queue, message.Id), 
                new[] { messageLengthBytes, messageBytes, message.Data }, 
                () => { return message.Delivered; },
                cancellationToken);
        }

        public Task DeleteMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken)
        {
            try
            {
                DeleteFile(GetMessagePath(user, queue, messageId));
            }
            catch
            {
                // must be file is deleting from some other place
            }

            return Task.CompletedTask;
        }

        public Task DeleteMessagesAsync(string user, string queue, CancellationToken cancellationToken)
        {
            DeleteDirectory(GetQueueMessagesPath(user, queue));
            return Task.CompletedTask;
        }

        public async Task<MessageEntity> LoadMessageAsync(string user, string queue, Guid messageId, bool loadDiskOnly, CancellationToken cancellationToken)
        {
            MessageEntity result = null;
            var path = GetMessagePath(user, queue, messageId);

            using (var file = File.OpenRead(path))
            {
                if (file.Length > 4)
                {
                    try
                    {
                        var messageLengthBytes = new byte[4];
                        await file.ReadAsync(messageLengthBytes, cancellationToken);
                        var messageLength = BitConverter.ToInt32(messageLengthBytes);

                        var messageBytes = new byte[messageLength];
                        await file.ReadAsync(messageBytes, cancellationToken);

                        result = (MessageEntity)_messageSerializer.Deserialize(messageBytes, typeof(MessageEntity));

                        if (4 + messageLength + result.DataLength != file.Length)
                        {
                            throw new IOException("Actual message length on disk is not equeal to stored message length.");
                        }

                        if (loadDiskOnly || result.Persistent != Persistence.DiskOnly)
                        {
                            var memory = new Memory<byte>(new byte[result.DataLength]);
                            await file.ReadAsync(memory, cancellationToken);
                            result.Data = memory;
                        }
                    }
                    catch (Exception)
                    {
                        result = null;
                    }
                }
            }

            if (result == null)
            {
                DeleteFile(path);
            }

            return result;
        }

        public Task<IEnumerable<Guid>> LoadMessageKeysAsync(string user, string queue, CancellationToken cancellationToken)
        {
            var result = LoadKeys(GetQueueMessagesPath(user, queue), "*.msg").Select(Guid.Parse);
            return Task.FromResult(result);
        }

        static private void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        static private void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static IEnumerable<string> LoadKeys(string path, string searchPattern = "*.json")
        {
            if (Directory.Exists(path))
            {
                var result = Directory.EnumerateFiles(path, searchPattern).Select(Path.GetFileNameWithoutExtension);

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
            try
            {
                var text = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);

                return JsonConvert.DeserializeObject<T>(text);
            }
            catch
            {
                return default;
            }
        }

        private static async Task WriteFileAsync(string path, string text, bool overwrite, CancellationToken cancellationToken)
        {
            if (overwrite || !File.Exists(path))
            {
                var write = async () => { await WriteAllTextAsync(path, text, cancellationToken); };

                try
                {
                    await write();
                }
                catch (DirectoryNotFoundException)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    await write();
                }
            }
        }

        private static async Task WriteFileAsync(string path, IEnumerable<ReadOnlyMemory<byte>> data, Func<bool> cancelFunc, CancellationToken cancellationToken)
        {
            if (!File.Exists(path))
            {
                var write = async () => { await WriteAllBytesAsync(path, data, cancelFunc, cancellationToken); };

                try
                {
                    await write();
                }
                catch (DirectoryNotFoundException)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    await write();
                }
            }
        }

        static async Task WriteAllTextAsync(string path, string text, CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(path, text, Encoding.UTF8, cancellationToken);
        }

        static async Task WriteAllBytesAsync(string path, IEnumerable<ReadOnlyMemory<byte>> data, Func<bool> cancelFunc, CancellationToken cancellationToken)
        {
            if (cancelFunc())
            {
                return;
            }

            using (var file = File.OpenWrite(path))
            {
                foreach (var chunk in data)
                {
                    if (!cancelFunc())
                    {
                        await file.WriteAsync(chunk, cancellationToken);
                    }
                }
            }

            if (cancelFunc())
            {
                try
                {
                    DeleteFile(path);
                }
                catch
                {
                    // must be file is deleting from some other place
                }
            }
        }

        private string GetUsersPath()
        {
            return Path.Combine(_baseClusterPath, UsersDirecotory);
        }

        private string GetUserPath(string user)
        {
            return Path.Combine(_baseClusterPath, UsersDirecotory, $"{user.ToLowerInvariant()}.json");
        }

        private string GetUserQueuesPath(string user)
        {
            return Path.Combine(_baseClusterPath, QueuesDirectory, user.ToLowerInvariant());
        }

        private string GetQueuePath(string user, string queue)
        {
            return Path.Combine(_baseClusterPath, QueuesDirectory, user.ToLowerInvariant(), $"{queue.ToLowerInvariant()}.json");
        }

        private string GetUserBindingsPath(string user)
        {
            return Path.Combine(_baseClusterPath, BindingsDirectory, user.ToLowerInvariant());
        }        

        private string GetBindingPath(string user, string exchange, string queue)
        {
            return Path.Combine(_baseClusterPath, BindingsDirectory, user.ToLowerInvariant(), $"{exchange.ToLowerInvariant()}-{queue.ToLowerInvariant()}.json");
        }

        private string GetBindingPath(string user, string binding)
        {
            return Path.Combine(_baseClusterPath, BindingsDirectory, user.ToLowerInvariant(), $"{binding.ToLowerInvariant()}.json");
        }

        private string GetUserMessagesPath(string user)
        {
            return Path.Combine(_basePath, MessagesDirectory, user.ToLowerInvariant());
        }

        private string GetQueueMessagesPath(string user, string queue)
        {
            return Path.Combine(_basePath, MessagesDirectory, user.ToLowerInvariant(), queue.ToLowerInvariant());
        }

        private string GetMessagePath(string user, string queue, Guid messageId)
        {
            return Path.Combine(_basePath, MessagesDirectory, user.ToLowerInvariant(), queue.ToLowerInvariant(), $"{messageId}.msg");
        }

        public void Dispose()
        {
        }
    }
}