using System;
using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public interface IResponse : ICommand
    {
        public Guid? RequestId { get; set; }

        public bool IsSuccess { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public List<string> Warnings { get; set; }
    }
}
