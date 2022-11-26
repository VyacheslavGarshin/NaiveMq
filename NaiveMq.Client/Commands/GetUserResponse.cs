using NaiveMq.Client.Entities;

namespace NaiveMq.Client.Commands
{
    public class GetUserResponse : AbstractResponse<GetUserResponse>
    {
        public UserEntity User { get; set; }
    }
}
