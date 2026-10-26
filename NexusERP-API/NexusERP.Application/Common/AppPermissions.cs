namespace NexusERP.Application.Common;

/// <summary>Module keys a <c>CustomRole</c> may list in its permissions; the frontend labels them.</summary>
public static class AppPermissions
{
    public static readonly IReadOnlyList<string> All =
    [
        "dashboard",
        "inventory",
        "warehouses",
        "stores",
        "products",
        "customers",
        "purchases",
        "sales",
        "pos",
        "hr",
        "finance",
        "reports",
    ];
}
