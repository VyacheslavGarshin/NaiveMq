namespace NaiveMq.Client.Commands
{
    public class GetUser : AbstractGetRequest<GetUserResponse>
    {
        public string Username { get; set; }
    }
}
