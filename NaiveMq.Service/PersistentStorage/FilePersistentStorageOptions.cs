﻿namespace NaiveMq.Service.PersistentStorage
{
    public class FilePersistentStorageOptions
    {
        public string Path { get; set; }

        public TimeSpan DeleteTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan DeleteRetryInterval { get; set; } = TimeSpan.FromMilliseconds(50);
    }
}