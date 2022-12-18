﻿namespace NaiveMq.Client.Commands
{
    public class Login : AbstractRequest<Confirmation>
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public Login()
        {
        }

        public Login(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Username))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Username) });
            }

            if (string.IsNullOrEmpty(Password))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Password) });
            }
        }
    }
}
