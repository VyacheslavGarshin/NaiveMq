using NaiveMq.Client.Enums;
using Newtonsoft.Json;
using System;

namespace NaiveMq.Client.Commands
{
    public class Message : AbstractRequest<MessageResponse>, IDataCommand
    {
        public string Queue { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>When receive message data is available only during handling in event.</remarks>
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public bool Request { get; set; }

        public Persistence Persistent { get; set; } = Persistence.No;

        public string RoutingKey { get; set; }

        public Message()
        {
        }

        public Message(string queue, ReadOnlyMemory<byte> data, bool request = false, Persistence persistent = Persistence.No, string routingKey = null)
        {
            Queue = queue;
            Data = data;
            Request = request;
            Persistent = persistent;
            RoutingKey = routingKey;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Queue))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Queue) });
            }

            if (Request && Persistent != Persistence.No)
            {
                throw new ClientException(ErrorCode.PersistentRequest);
            }

            if (Request && !Confirm)
            {
                throw new ClientException(ErrorCode.RequestConfirmRequred);
            }

            if (Data.Length == 0)
            {
                throw new ClientException(ErrorCode.DataIsEmpty);
            }
        }
    }
}
