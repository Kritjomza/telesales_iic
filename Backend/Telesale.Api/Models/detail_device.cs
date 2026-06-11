using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class detail_device
{
    public uint id { get; set; }

    public uint dtl_id { get; set; }

    public int? equipment_id { get; set; }

    public int? equipment_qty { get; set; }

    public DateOnly? equipment_expire { get; set; }

    public string? equipment_dtl { get; set; }

    public int? competitor_id { get; set; }

    public string? progress_status { get; set; }

    public int count_renewal { get; set; }

    public int point { get; set; }

    public int bak_point { get; set; }

    public int? dtl_dv_id { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public int? desktop_qty { get; set; }

    public int? server_qty { get; set; }

    public int? category_id { get; set; }

    public int? brand_id { get; set; }

    public int? cost_sheet { get; set; }

    public virtual detail dtl { get; set; } = null!;
}
