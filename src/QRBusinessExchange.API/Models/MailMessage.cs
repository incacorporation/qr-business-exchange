namespace QRBusinessExchange.API.Models
{
    internal class MailMessage
    {
        public string[] ToRecipients { get; set; } = new string[0];

        public string[] CcRecipients { get; set; } = new string[0];

        public string[] BccRecipients { get; set; } = new string[0];
        public string? SentDateTime { get; set; }

        public string? Subject { get; set; }

        public string? Body { get; set; }
    }
}
