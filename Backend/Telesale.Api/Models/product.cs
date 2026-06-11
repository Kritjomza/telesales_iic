using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class product
{
    public uint id { get; set; }

    public string name { get; set; } = null!;

    public uint? categories_id { get; set; }

    public uint? brands_id { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual brand? brands { get; set; }

    public virtual category? categories { get; set; }
}
