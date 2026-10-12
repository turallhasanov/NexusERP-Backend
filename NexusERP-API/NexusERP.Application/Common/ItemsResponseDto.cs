namespace NexusERP.Application.Common;

/// <summary>List responses: { "ok": true, "items": [ ... ] }</summary>
public sealed record ItemsResponseDto<T>(IReadOnlyList<T> Items) : SuccessResponse;
