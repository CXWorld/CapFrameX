using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<ContactController> _logger;

        public ContactController(IOptions<SmtpOptions> options, ILogger<ContactController> logger)
        {
            _options = options.Value ?? throw new InvalidOperationException("Cannot send Mail without Credentials");
            _logger = logger;
        }

		[HttpPost]
        public async Task<IActionResult> Post(ContactMessage model)
        {
			using (var smtpClient = new SmtpClient(_options.Host, _options.Port)
			{
				UseDefaultCredentials = false,
				Credentials = new NetworkCredential(_options.Username, _options.Password),
				EnableSsl = _options.UseSTARTTLS,
				DeliveryMethod = SmtpDeliveryMethod.Network,
			})
			{
				var message = new MailMessage()
				{
					Subject = model.Subject,
					Body = model.Message,
					IsBodyHtml = true,
				};
				foreach(var receiver in _options.Recipients)
                {
					message.To.Add(receiver);
                }
				message.From = new MailAddress(_options.From);
				message.ReplyToList.Add(new MailAddress(model.Email, model.Name));

				_logger.LogDebug("Attempting to send Email:\n{mail}", model);
				await smtpClient.SendMailAsync(message);
				_logger.LogInformation("Sent Email '{subject}' to {receipients}", message.Subject, string.Join(", ", message.To.Select(t => t.Address)));

				return Ok();
			}
		}
    }
}
