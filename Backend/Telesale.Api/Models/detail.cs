using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class detail
{
    public uint id { get; set; }

    public string? contact_name { get; set; }

    public string? contact_email { get; set; }

    public string? contact_tel { get; set; }

    public string? contact_position { get; set; }

    public string? contact_tel_office { get; set; }

    public int? profit_last_year { get; set; }

    public int? user_cnt { get; set; }

    public int bak_point { get; set; }

    public uint cust_id { get; set; }

    public int point { get; set; }

    public int total_point { get; set; }

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual customer cust { get; set; } = null!;

    public virtual ICollection<detail_device> detail_devices { get; set; } = new List<detail_device>();

    public virtual ICollection<detail_pj> detail_pjs { get; set; } = new List<detail_pj>();
}
