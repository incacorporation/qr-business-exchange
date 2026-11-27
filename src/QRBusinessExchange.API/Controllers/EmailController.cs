using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.API.Models;
using MailKit.Net.Pop3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace QRBusinessExchange.API.Controllers
{
	//[EnableCors("MyCORSPolicy")]
	[Route("[controller]")]
	[ApiController]
	[Authorize(AuthenticationSchemes = "Bearer")]
	public class EmailController : ControllerBase
	{
		// POST /email/send
		[HttpPost("Send")]
		public async Task<IActionResult> SendEmail([FromBody] string data)
		{
			QuoteRequestModel request = data.Decode().Deserialize<QuoteRequestModel>();

			var message = new MimeMessage();
			message.From.Add(new MailboxAddress($"{request.FirstName} {request.LastName} <{request.Email}>", request.Email));
			message.To.Add(new MailboxAddress("Pool Builders", "poolbuilders@edupoolservices.com"));
			message.Subject = $"Quote Request from {request.CompanyName}";

			// Plain text body
			var bodyBuilder = new BodyBuilder
			{
				HtmlBody = $@"
					<h2>New Quote Request</h2>
					<p><strong>Project Location:</strong> {request.ProjectLocation}</p>
					<p><strong>First Name:</strong> {request.FirstName}</p>
					<p><strong>Last Name:</strong> {request.LastName}</p>
					<p><strong>Company Name:</strong> {request.CompanyName}</p>
					<p><strong>Email:</strong> {request.Email}</p>
					<p><strong>Phone:</strong> {request.Phone}</p>
					<p><strong>Message:</strong><br/>{request.Message}</p>"
			};
			message.Body = bodyBuilder.ToMessageBody();

			using (var client = new System.Net.Mail.SmtpClient())
			{
				// Accept all SSL certificates (optional, not for production!)
				// Note: System.Net.Mail.SmtpClient doesn't support cert validation callback

				// Connect to Plesk mail server (change host, port)
				var smtpHost = "SmtpHost".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("SMTP host is not configured.");
				var smtpPortString = "SmtpPort".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("SMTP port is not configured.");
				if (!int.TryParse(smtpPortString, out var smtpPort))
					throw new InvalidOperationException("SMTP port is not a valid integer.");

				client.Host = smtpHost;
				client.Port = smtpPort;
				client.EnableSsl = true;

				// Authenticate with your Plesk mailbox credentials
				var SmtpUsername = "SmtpUsername".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("SMTP Username is not configured.");
				var SmtpPassword = "SmtpPassword".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("SMTP Password is not configured.");
				client.Credentials = new NetworkCredential(SmtpUsername, SmtpPassword);

				// Convert MimeMessage to MailMessage
				var mailMessage = new System.Net.Mail.MailMessage();
				mailMessage.From = new MailAddress(message.From.ToString());
				foreach (var to in message.To)
				{
					mailMessage.To.Add(to.ToString());
				}
				mailMessage.Subject = message.Subject;
				mailMessage.Body = message.HtmlBody ?? message.TextBody;
				mailMessage.IsBodyHtml = !string.IsNullOrEmpty(message.HtmlBody);

				// Send the email
				await client.SendMailAsync(mailMessage);
			}

			return Ok("Success");
		}

		// GET /email/receive
		[HttpGet("Receive")]
		public async Task<IActionResult> ReceiveEmail()/*[FromQuery] EmailReceiveRequest request*/
		{
			try
			{
				// Replace these with your actual values
				// Connect to Plesk mail server (change host, port)
				var pop3Host = "Pop3Host".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("Pop3 host is not configured.");
				var pop3PortString = "Pop3Port".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("Pop3 port is not configured.");
				if (!int.TryParse(pop3PortString, out var pop3Port))
					throw new InvalidOperationException("Pop3 port is not a valid integer.");

				bool useSsl = true;                     // Most servers require SSL
				var username = "SmtpUsername".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("SMTP Username is not configured.");
				var password = "SmtpPassword".ReadFromConfiguration("MailServer") ?? throw new InvalidOperationException("SMTP Password is not configured.");

				List<ReceivedMailMessage> messages = new List<ReceivedMailMessage>();
				using (var client = new Pop3Client())
				{
					client.Connect(pop3Host, pop3Port, useSsl);
					client.Authenticate(username, password);

					for (int i = 0; i < client.Count; i++)
					{
						MimeMessage message = client.GetMessage(i);
						ReceivedMailMessage receivedMessage = new ReceivedMailMessage
						{
							Subject = message.Subject,
							From = message.From.ToString(),
							ReceivedDateTime = message.Date.DateTime,
							Body = message.TextBody == null ? message.HtmlBody : message.TextBody,
						};
						messages.Add(receivedMessage);
						Console.WriteLine($"Subject: {message.Subject}");
						Console.WriteLine($"From: {message.From}");
						Console.WriteLine($"Date: {message.Date}");
						Console.WriteLine($"Text Body: {message.TextBody}");
						Console.WriteLine("---");
					}

					client.Disconnect(true);
				}
				return Ok(messages);
			}
			catch (Exception exc)
			{
				return BadRequest(exc.Message);
			}
		}
	}

	public class EmailSendRequest
	{
		public string FromName { get; set; }
		public string FromEmail { get; set; }
		public string ToName { get; set; }
		public string ToEmail { get; set; }
		public string Subject { get; set; }
		public string Body { get; set; }
		public string SmtpHost { get; set; }
		public int SmtpPort { get; set; }
		public string SmtpUsername { get; set; }
		public string SmtpPassword { get; set; }
		//public bool UseSsl { get; set; }
	}

	public class EmailReceiveRequest
	{
		public string Pop3Host { get; set; }
		public int Pop3Port { get; set; }
		public string Pop3Username { get; set; }
		public string Pop3Password { get; set; }
		//public bool UseSsl { get; set; }
	}
}
