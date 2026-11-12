using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QRBusinessExchange.API.Models
{
	public class QuoteRequestModel
	{
		public string? ProjectLocation { get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? CompanyName { get; set; }
		public string? Email { get; set; }
		public string? Phone { get; set; }
		public string? Message { get; set; }
	}
}
