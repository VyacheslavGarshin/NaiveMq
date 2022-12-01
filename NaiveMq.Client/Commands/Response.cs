using System;

namespace NaiveMq.Client.Commands
{
    public class Response : AbstractRequest<Confirmation>
    {
        public Guid RequestId { get; set; }

        public bool Success { get; set; } = true;

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public string Text { get; set; }

        public static Response Ok(Guid requestId, string text)
        {
            return new Response
            {
                RequestId = requestId,
                Success = true,
                Text = text,
            };
        }

        public static Response Error(Guid requestId, string errorCode, string errorMessage)
        {
            return new Response
            {
                RequestId = requestId,
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
            };
        }
    }
}
