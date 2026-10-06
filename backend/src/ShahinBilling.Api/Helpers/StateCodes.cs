namespace ShahinBilling.Api.Helpers;

/// <summary>Indian GST state codes. The first 2 digits of a GSTIN are the state code.</summary>
public static class StateCodes
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["01"] = "Jammu and Kashmir", ["02"] = "Himachal Pradesh", ["03"] = "Punjab",
        ["04"] = "Chandigarh", ["05"] = "Uttarakhand", ["06"] = "Haryana", ["07"] = "Delhi",
        ["08"] = "Rajasthan", ["09"] = "Uttar Pradesh", ["10"] = "Bihar", ["11"] = "Sikkim",
        ["12"] = "Arunachal Pradesh", ["13"] = "Nagaland", ["14"] = "Manipur", ["15"] = "Mizoram",
        ["16"] = "Tripura", ["17"] = "Meghalaya", ["18"] = "Assam", ["19"] = "West Bengal",
        ["20"] = "Jharkhand", ["21"] = "Odisha", ["22"] = "Chhattisgarh", ["23"] = "Madhya Pradesh",
        ["24"] = "Gujarat", ["25"] = "Daman and Diu (old)",
        ["26"] = "Dadra and Nagar Haveli and Daman and Diu", ["27"] = "Maharashtra",
        ["28"] = "Andhra Pradesh (old)", ["29"] = "Karnataka", ["30"] = "Goa", ["31"] = "Lakshadweep",
        ["32"] = "Kerala", ["33"] = "Tamil Nadu", ["34"] = "Puducherry",
        ["35"] = "Andaman and Nicobar Islands", ["36"] = "Telangana", ["37"] = "Andhra Pradesh",
        ["38"] = "Ladakh", ["97"] = "Other Territory", ["99"] = "Centre Jurisdiction"
    };

    public static string NameOf(string code) => All.TryGetValue(code ?? "", out var n) ? n : "";

    public static string FromGstin(string? gstin) =>
        !string.IsNullOrWhiteSpace(gstin) && gstin.Length >= 2 && All.ContainsKey(gstin[..2]) ? gstin[..2] : "";
}
