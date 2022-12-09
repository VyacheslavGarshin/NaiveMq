using NaiveMq.Client.Dto;

namespace NaiveMq.Client.Commands
{
    public class GetUserResponse : AbstractResponse<GetUserResponse>
    {
        public User User { get; set; }
    }
}
