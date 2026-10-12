namespace NexusERP.Application.Common;

/// <summary>Compact reference to a coded dictionary entry (warehouse, customer, ...) embedded in other responses.</summary>
public sealed record CodeNameDto(string Id, string Code, string Name);

/// <summary>Compact product reference embedded in document lines and stock balances.</summary>
public sealed record ProductRefDto(string Id, string Code, string Name, string Barcode, string UnitShortName);
