﻿namespace NaiveMq.Client.Commands
{
    public class Login : AbstractRequest<Confirmation>
    {
        public string Username { get; set; }

        public string Password { get; set; }
    }
}