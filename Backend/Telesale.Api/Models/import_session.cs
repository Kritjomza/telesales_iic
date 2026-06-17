using System;

namespace Telesale.Api.Models;

public partial class import_session
{
    public uint id { get; set; }

    public uint imported_by { get; set; }

    public string? file_name { get; set; }

    public int total_rows { get; set; }

    public int imported_rows { get; set; }

    public int skipped_rows { get; set; }

    public int error_rows { get; set; }

    public string? errors_json { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
