﻿using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class DeleteUser : AbstractRequest<Confirmation>, IReplicable
    {
        [DataMember(Name = "U")]
        public string Username { get; set; }

        public DeleteUser()
        {
        }

        public DeleteUser(string username)
        {
            Username = username;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Username))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Username) });
            }
        }
    }
}
