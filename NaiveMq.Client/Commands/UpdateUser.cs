namespace NaiveMq.Client.Commands
{
    public class UpdateUser : AbstractRequest<Confirmation>
    {
        public string Username { get; set; }

        public bool Administrator { get; set; }

        /// <summary>
        /// Update password if not empty.
        /// </summary>
        public string Password { get; set; }
    }
}
