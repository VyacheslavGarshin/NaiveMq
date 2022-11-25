﻿using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Entities;

namespace NaiveMq.Service.Cogs
{
    public class HandlerContext
    {
        public Storage Storage { get; set; }

        public NaiveMqClient Client { get; set; }

        public ILogger Logger { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public UserEntity User { get; set; }

        /// <summary>
        /// True in case handler is called on reinstating persistent data.
        /// </summary>
        public bool Reinstate { get; set; }
    }
}
