using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class target
{
    public uint id { get; set; }

    public int? user_id { get; set; }

    public DateOnly? dt { get; set; }

    public string? target_type { get; set; }

    public int point { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
