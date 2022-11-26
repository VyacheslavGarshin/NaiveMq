namespace NaiveMq.Client.Commands
{
    public class ChangePassword : AbstractRequest<Confirmation>
    {
        public string CurrentPassword { get; set; }
        
        public string NewPassword { get; set; }
    }
}
