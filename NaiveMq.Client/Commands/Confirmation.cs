using System;

namespace NaiveMq.Client.Commands
{
    public class Confirmation : AbstractResponse<Confirmation>
    {
        public string Text { get; set; }

        public static Confirmation Ok(Guid requestId, string text = null)
        {
            return new Confirmation
            {
                RequestId = requestId,
                Success = true,
                Text = text,
            };
        }
    }
}
