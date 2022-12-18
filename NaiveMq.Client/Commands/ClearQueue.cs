﻿namespace NaiveMq.Client.Commands
{
    public class ClearQueue : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }

        public ClearQueue()
        {
        }

        public ClearQueue(string name)
        {
            Name = name;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Name))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Name) });
            }
        }
    }
}