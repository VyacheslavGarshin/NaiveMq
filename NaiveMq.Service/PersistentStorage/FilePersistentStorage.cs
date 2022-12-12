﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaiveMq.Service.Entities;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace NaiveMq.Service.PersistentStorage
{
    public class FilePersistentStorage : IPersistentStorage
    {
        private const string MessagesDirectory = "messages";
        
        private const string BindingsDirectory = "bindings";

        private const string QueuesDirectory = "queues";

        private const string UsersDirecotory = "users";

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
            await DeleteFileAsync(GetUserPath(user), true, cancellationToken);
            await DeleteDirectoryAsync(GetUserQueuesPath(user), false, cancellationToken);
            await DeleteDirectoryAsync(GetUserBindingsPath(user), false, cancellationToken);
            await DeleteDirectoryAsync(GetUserMessagesPath(user), false, cancellationToken);
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

        public async Task DeleteQueueAsync(string user, string queue, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetQueuePath(user, queue), true, cancellationToken);
            await DeleteDirectoryAsync(GetQueueMessagesPath(user, queue), false, cancellationToken);
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
            await DeleteFileAsync(GetBindingPath(user, exchange, queue), true, cancellationToken);
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
            var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
            var messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);

            await WriteFileAsync(GetMessagePath(user, queue, message.Id), new[] { messageLengthBytes, messageBytes, message.Data }, cancellationToken);
        }

        public async Task DeleteMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken)
        {
            await DeleteFileAsync(GetMessagePath(user, queue, messageId), true, cancellationToken);
        }

        public async Task<MessageEntity> LoadMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken)
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

                        result = JsonConvert.DeserializeObject<MessageEntity>(Encoding.UTF8.GetString(messageBytes));
                        
                        var memory = new Memory<byte>(new byte[result.DataLength]);
                        await file.ReadAsync(memory, cancellationToken);
                        result.Data = memory;

                        if (result.Data.Length != result.DataLength)
                        {
                            throw new IOException("Message data length is not equeal to DataLength property of the message.");
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
                await DeleteFileAsync(path, false, cancellationToken);
            }

            return result;
        }

        public Task<IEnumerable<Guid>> LoadMessageKeysAsync(string user, string queue, CancellationToken cancellationToken)
        {
            var result = LoadKeys(GetQueueMessagesPath(user, queue), "*.msg").Select(Guid.Parse);
            return Task.FromResult(result);
        }

        private async Task DeleteFileAsync(string path, bool waitExists, CancellationToken cancellationToken)
        {
            await DeleteAsync(path, () =>
            {
                // wait for file to appear. antivirus can meddle and subscription takes message earlier than it's written to disk.
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                else
                {
                    return !waitExists;
                }
            }, cancellationToken);
        }

        private async Task DeleteDirectoryAsync(string path, bool waitExists, CancellationToken cancellationToken)
        {
            await DeleteAsync(path, () =>
            {
                // wait for file to appear. antivirus can meddle and subscription takes message earlier than it's written to disk.
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
                else
                {
                    return !waitExists;
                }
            }, cancellationToken);
        }

        private async Task DeleteAsync(string path, Func<bool> action, CancellationToken cancellationToken)
        {
            var sw = new Stopwatch();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (action())
                    {
                        return;
                    }
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

        private static async Task WriteFileAsync(string path, IEnumerable<ReadOnlyMemory<byte>> data, CancellationToken cancellationToken)
        {
            if (!File.Exists(path))
            {
                try
                {
                    await WriteAllBytesAsync(path, data, cancellationToken);
                }
                catch (DirectoryNotFoundException)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    await WriteAllBytesAsync(path, data, cancellationToken);
                }
            }
        }

        static async Task WriteAllTextAsync(string path, string text, CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(path, text, Encoding.UTF8, cancellationToken);
        }

        static async Task WriteAllBytesAsync(string path, IEnumerable<ReadOnlyMemory<byte>> data, CancellationToken cancellationToken)
        {
            using var file = File.OpenWrite(path);

            foreach (var chunk in data)
            {
                await file.WriteAsync(chunk, cancellationToken);
            }
        }

        private string GetUsersPath()
        {
            return Path.Combine(_basePath, UsersDirecotory);
        }

        private string GetUserPath(string user)
        {
            return Path.Combine(_basePath, UsersDirecotory, $"{user.ToLowerInvariant()}.json");
        }

        private string GetUserQueuesPath(string user)
        {
            return Path.Combine(_basePath, QueuesDirectory, user.ToLowerInvariant());
        }

        private string GetQueuePath(string user, string queue)
        {
            return Path.Combine(_basePath, QueuesDirectory, user.ToLowerInvariant(), $"{queue.ToLowerInvariant()}.json");
        }

        private string GetUserBindingsPath(string user)
        {
            return Path.Combine(_basePath, BindingsDirectory, user.ToLowerInvariant());
        }        

        private string GetBindingPath(string user, string exchange, string queue)
        {
            return Path.Combine(_basePath, BindingsDirectory, user.ToLowerInvariant(), $"{exchange.ToLowerInvariant()}-{queue.ToLowerInvariant()}.json");
        }

        private string GetBindingPath(string user, string binding)
        {
            return Path.Combine(_basePath, BindingsDirectory, user.ToLowerInvariant(), $"{binding.ToLowerInvariant()}.json");
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