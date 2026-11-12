using System;
using System.Collections.Generic;

namespace QRBusinessExchange.API.Models;

public partial class ReceivedMailMessage
{
    public string Id { get; set; } = null!;

    public DateTime ReceivedDateTime { get; set; }

    public string? From { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public bool? HasAttachments { get; set; }

    public string? Createdby { get; set; }

    public DateTime? Createdon { get; set; }
}
