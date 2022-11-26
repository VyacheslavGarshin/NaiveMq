﻿namespace NaiveMq.Client.Commands
{
    public class AddUser : AbstractRequest<Confirmation>
    {
        public string Username { get; set; }

        public bool IsAdministrator { get; set; }

        public string Password { get; set; }
    }
}
