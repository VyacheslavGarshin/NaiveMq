using NaiveMq.Client.Enums;
using System;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Send queue message.
    /// </summary>
    public class Message : AbstractRequest<MessageResponse>, IDataCommand
    {
        /// <summary>
        /// Queue.
        /// </summary>
        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>When receive message data is available only during handling in event.</remarks>
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }
        
        /// <summary>
        /// Mark message as request and wait for an answer from consumer.
        /// </summary>
        [DataMember(Name = "R")]
        public bool Request { get; set; }

        /// <summary>
        /// Save message.
        /// </summary>
        [DataMember(Name = "P")]
        public Persistence Persistent { get; set; } = Persistence.No;

        /// <summary>
        /// Exchange routing key.
        /// </summary>
        [DataMember(Name = "RK")]
        public string RoutingKey { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public Message()
        {
        }

        /// <summary>
        /// Constructor with params.
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="data"></param>
        /// <param name="request"></param>
        /// <param name="persistent"></param>
        /// <param name="routingKey"></param>
        public Message(string queue, ReadOnlyMemory<byte> data, bool request = false, Persistence persistent = Persistence.No, string routingKey = null)
        {
            Queue = queue;
            Data = data;
            Request = request;
            Persistent = persistent;
            RoutingKey = routingKey;
        }

        /// <inheritdoc/>
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
