using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class detail_pj
{
    public uint id { get; set; }

    public uint dtl_id { get; set; }

    public string? dtl { get; set; }

    public DateOnly? close_date { get; set; }

    public string? contact { get; set; }

    public string? tel { get; set; }

    public int point { get; set; }

    public int bak_point { get; set; }

    public string progress_status { get; set; } = null!;

    public double? amt { get; set; }

    public double? profit { get; set; }

    public int? competitor_id { get; set; }

    public string? reason { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual detail dtlNavigation { get; set; } = null!;
}
