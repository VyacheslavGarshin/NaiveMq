﻿using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Change password for current user.
    /// </summary>
    public class ChangePassword : AbstractRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Current password.
        /// </summary>
        [DataMember(Name = "CP")]
        public string CurrentPassword { get; set; }

        /// <summary>
        /// A new one.
        /// </summary>
        [DataMember(Name = "NP")]
        public string NewPassword { get; set; }

        /// <summary>
        /// Creates new ChangePassword command.
        /// </summary>
        public ChangePassword()
        {
        }

        /// <summary>
        /// Creates new ChangePassword command with params.
        /// </summary>
        /// <param name="currentPassword"></param>
        /// <param name="newPassword"></param>
        public ChangePassword(string currentPassword, string newPassword)
        {
            CurrentPassword = currentPassword;
            NewPassword = newPassword;
        }

        /// <inheritdoc/>
        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(CurrentPassword))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(CurrentPassword) });
            }

            if (string.IsNullOrEmpty(NewPassword))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(NewPassword) });
            }
        }
    }
}
