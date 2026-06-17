using System;

namespace Telesale.Api.Models;

public partial class import_row
{
    public uint id { get; set; }

    public uint session_id { get; set; }

    public string row_data_json { get; set; } = null!;

    public string status { get; set; } = null!; // "Imported", "Updated", "Skipped", "Error"

    public string? error_message { get; set; }

    public DateTime? created_at { get; set; }
}
