using NaiveMq.Client.Dto;

namespace NaiveMq.Client.Commands
{
    public class GetProfileResponse : AbstractResponse<GetProfileResponse>
    {
        public Profile Profile { get; set; }
    }
}
