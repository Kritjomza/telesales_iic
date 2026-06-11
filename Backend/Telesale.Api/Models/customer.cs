using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class customer
{
    public uint id { get; set; }

    public string? code { get; set; }

    public string? name { get; set; }

    public string? address { get; set; }

    public string? phone { get; set; }

    public double? capital { get; set; }

    public int? owner_id { get; set; }

    public int? business_type_id { get; set; }

    public bool? is_active { get; set; }

    public string status { get; set; } = null!;

    public string create_type { get; set; } = null!;

    public int? telesale_id { get; set; }

    public int? sale_id { get; set; }

    public int? updated_user { get; set; }

    public DateOnly? start_dt { get; set; }

    public DateOnly? sale_assign_dt { get; set; }

    public bool is_assign_sale { get; set; }

    public bool is_assign_telesale { get; set; }

    public int? telesale_id_bak { get; set; }

    public int? sale_id_bak { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual ICollection<detail> details { get; set; } = new List<detail>();
}
