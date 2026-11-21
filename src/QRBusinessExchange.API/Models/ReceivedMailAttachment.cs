using System;
using System.Collections.Generic;

namespace QRBusinessExchange.API.Models;

public partial class ReceivedMailAttachment
{
    public Guid Id { get; set; }

    public string? ReceivedMailId { get; set; }

    public string? Attachment { get; set; }

    public string? Createdby { get; set; }

    public DateTime? Createdon { get; set; }
}
