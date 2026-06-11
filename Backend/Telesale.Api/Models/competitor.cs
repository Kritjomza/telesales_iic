using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class competitor
{
    public uint id { get; set; }

    public string year { get; set; } = null!;

    public double amt { get; set; }

    public string compare { get; set; } = null!;

    public string name { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
