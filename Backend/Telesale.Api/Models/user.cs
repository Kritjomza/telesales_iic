using System;
using System.Collections.Generic;

namespace Telesale.Api.Models;

public partial class user
{
    public uint id { get; set; }

    public string name { get; set; } = null!;

    public string username { get; set; } = null!;

    public string email { get; set; } = null!;

    public string roles { get; set; } = null!;

    public string password { get; set; } = null!;

    public bool? is_active { get; set; }

    public string? linetoken { get; set; }

    public string? position { get; set; }

    public string? tel { get; set; }

    public string? remember_token { get; set; }

    public int failed_login_count { get; set; }

    public DateTime? locked_until { get; set; }

    public DateTime? last_login_at { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
