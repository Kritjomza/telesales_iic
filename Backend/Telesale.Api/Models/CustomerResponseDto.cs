using System;

namespace Telesale.Api.Models;

public class CustomerResponseDto
{
    public uint id { get; set; }
    public string name { get; set; } = null!;
    public string address { get; set; } = "";
    public string capital { get; set; } = "0";
    public int? telesale_id { get; set; }
    public string? telesale { get; set; }
    public int? sale_id { get; set; }
    public string? sale { get; set; }
    public string status { get; set; } = null!;
    public bool is_active { get; set; }
    public string? start_dt { get; set; }
    public string bt_type { get; set; } = null!;
    public int renewalDays { get; set; }
    public bool hasCostSheet { get; set; }
    public string updatedAt { get; set; } = "";
    public string? phone { get; set; }
    public string? subdistrict { get; set; }
    public string? district { get; set; }
    public string? province { get; set; }
    public string? postal_code { get; set; }
    public string? primary_contact_name { get; set; }
    public string? primary_contact_tel { get; set; }
    public string? primary_contact_email { get; set; }
    public string? primary_contact_tel_office { get; set; }
    public bool? hasProductLicenseInfo { get; set; }
    public string? matchedField { get; set; }
}

public class ContactResponseDto
{
    public uint id { get; set; }
    public uint cust_id { get; set; }
    public string contact_name { get; set; } = "";
    public string contact_email { get; set; } = "";
    public string contact_tel { get; set; } = "";
    public string contact_tel_office { get; set; } = "";
    public int point { get; set; }
    public int total_point { get; set; }
}

public class DeviceResponseDto
{
    public uint id { get; set; }
    public uint dtl_id { get; set; }
    public string full_name { get; set; } = "";
    public string full_name2 { get; set; } = "";
    public string dtl { get; set; } = "";
    public int desktop_qty { get; set; }
    public int server_qty { get; set; }
    public string? equipment_expire { get; set; }
    public int point { get; set; }
    public string progress_status { get; set; } = "New";
    public string competitor_name { get; set; } = "";
}

public class ProjectResponseDto
{
    public uint id { get; set; }
    public uint dtl_id { get; set; }
    public string dtl { get; set; } = "";
    public string? close_date { get; set; }
    public int point { get; set; }
    public string progress_status { get; set; } = "New";
}

public class CostSheetResponseDto
{
    public uint id { get; set; }
    public int cust_id { get; set; }
    public string project_name { get; set; } = "";
    public int brand_id { get; set; }
    public int product_id { get; set; }
    public int qty { get; set; }
    public double cost_price { get; set; }
    public double sale_price { get; set; }
    public double discount { get; set; }
    public int margin { get; set; }
    public double gp_amount { get; set; }
    public double gp_percent { get; set; }
    public int owner_share_percent { get; set; }
    public int employee_share_percent { get; set; }
    public string status { get; set; } = "Pending";
    public string created_at { get; set; } = "";
}
