using NaiveMq.Client.Enums;
using Newtonsoft.Json;
using System;

namespace NaiveMq.Client.Commands
{
    public class Message : AbstractRequest<Confirmation>, IDataCommand
    {
        public string Queue { get; set; }

        public bool Request { get; set; }

        public Persistence Persistent { get; set; } = Persistence.No;

        public string RoutingKey { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>When receive message data is available only during handling in event.</remarks>
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (Request && Persistent != Persistence.No)
            {
                throw new ClientException(ErrorCode.PersistentRequest);
            }

            if (Data.Length == 0)
            {
                throw new ClientException(ErrorCode.DataIsEmpty);
            }
        }
    }
}
