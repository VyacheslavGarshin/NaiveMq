using System;

namespace NaiveMq.Client.Commands
{
    public interface IResponse : ICommand
    {
        public Guid RequestId { get; set; }

        public string RequestTag { get; set; }

        public bool Success { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }
    }
}
