using NaiveMq.Client.Dto;

namespace NaiveMq.Client.Commands
{
    public class GetUserResponse : AbstractResponse<GetUserResponse>
    {
        public UserDto User { get; set; }
    }
}
