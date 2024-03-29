﻿using NaiveMq.Client;
using NaiveMq.Client.Cogs;

namespace NaiveMq.Service
{
    public class ServerException : Exception
    {
        public ErrorCode ErrorCode { get; set; }

        public ServerException(ErrorCode errorCode) : this(errorCode, errorCode.GetDescription())
        {
        }

        public ServerException(ErrorCode errorCode, object[] args) : this(errorCode, string.Format(errorCode.GetDescription(), args))
        {
        }

        public ServerException(ErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
