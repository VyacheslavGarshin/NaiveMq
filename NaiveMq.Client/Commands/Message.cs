using NaiveMq.Client.Enums;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class Message : AbstractRequest<MessageResponse>, IDataCommand
    {
        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>When receive message data is available only during handling in event.</remarks>
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        [DataMember(Name = "R")]
        public bool Request { get; set; }

        [DataMember(Name = "P")]
        public Persistence Persistent { get; set; } = Persistence.No;

        [DataMember(Name = "RK")]
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
