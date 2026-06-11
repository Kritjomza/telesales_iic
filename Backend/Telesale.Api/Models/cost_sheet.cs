using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class cost_sheet
{
    public uint id { get; set; }

    public string? attention { get; set; }

    public string? company { get; set; }

    public string? address { get; set; }

    public string? email { get; set; }

    public string? qo_no { get; set; }

    public DateOnly? dt { get; set; }

    public string? tel { get; set; }

    public string? fax { get; set; }

    public string? revised_no { get; set; }

    public string? details { get; set; }

    public int desktop_qty { get; set; }

    public int server_qty { get; set; }

    public string? brand { get; set; }

    public string? edition { get; set; }

    public int margin { get; set; }

    public string status { get; set; } = null!;

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
