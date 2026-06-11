using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class profile
{
    public uint id { get; set; }

    public string name { get; set; } = null!;

    public string type { get; set; } = null!;

    public string? items { get; set; }

    public string? editions { get; set; }

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
