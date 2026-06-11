using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class antivirus_service_list
{
    public uint id { get; set; }

    public string? name { get; set; }

    public string? detail { get; set; }

    public int margin { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
