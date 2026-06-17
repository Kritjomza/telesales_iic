using System;

namespace Telesale.Api.Models;

public partial class assignment_history
{
    public uint id { get; set; }

    public uint customer_id { get; set; }

    public int? old_sale_id { get; set; }

    public int? new_sale_id { get; set; }

    public int? old_telesale_id { get; set; }

    public int? new_telesale_id { get; set; }

    public uint? changed_by_id { get; set; }

    public DateTime? changed_at { get; set; }

    public string? reason { get; set; }

    public virtual customer customer { get; set; } = null!;
}
