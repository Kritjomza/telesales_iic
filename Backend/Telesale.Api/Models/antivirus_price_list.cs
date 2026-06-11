using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class antivirus_price_list
{
    public uint id { get; set; }

    public string? brand { get; set; }

    public string? code { get; set; }

    public string? edition { get; set; }

    public int start { get; set; }

    public int end { get; set; }

    public double cost { get; set; }

    public string types { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
