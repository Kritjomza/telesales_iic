using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class business_type
{
    public uint id { get; set; }

    public string? type { get; set; }

    public string? dtl { get; set; }

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
