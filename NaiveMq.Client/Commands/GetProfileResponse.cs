using NaiveMq.Client.Entities;

namespace NaiveMq.Client.Commands
{
    public class GetProfileResponse : AbstractResponse<GetProfileResponse>
    {
        public ProfileEntity Profile { get; set; }
    }
}
