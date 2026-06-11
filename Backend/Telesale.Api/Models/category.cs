using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class category
{
    public uint id { get; set; }

    public string name { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual ICollection<product> products { get; set; } = new List<product>();
}
