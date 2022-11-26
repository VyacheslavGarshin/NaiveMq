namespace NaiveMq.Client.Commands
{
    public class GetUser : AbstractRequest<GetUserResponse>
    {
        public string Username { get; set; }

        /// <summary>
        /// Try to get user.
        /// </summary>
        /// <remarks>Return null if user is not found. Overwise raise an exception. True by default.</remarks>
        public bool Try { get; set; } = true;
    }
}
