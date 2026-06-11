using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class password_reset
{
    public string email { get; set; } = null!;

    public string token { get; set; } = null!;

    public DateTime created_at { get; set; }
}
