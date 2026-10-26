using System.Linq.Expressions;
using NexusERP.Application.Common;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Employees;

/// <summary>Fields are nullable so missing values reach our own validation; <c>storeId</c> is optional.</summary>
public sealed record CreateEmployeeRequestDto(string? FullName, string? Title, string? Department, DateOnly? HiredAt, decimal? Salary, string? StoreId = null);

/// <summary>Body of PUT /api/employees/{id}: the whole profile; <c>storeId</c> null means company-wide staff.</summary>
public sealed record UpdateEmployeeRequestDto(string? FullName, string? Title, string? Department, DateOnly? HiredAt, decimal? Salary, string? StoreId);

/// <summary>Body of POST /api/employees/{id}/vacation. <c>leaveType</c> defaults to annual leave.</summary>
public sealed record VacationRequestDto(DateOnly? StartDate, DateOnly? EndDate, LeaveType? LeaveType, string? Note);

/// <summary>Body of POST /api/employees/{id}/terminate. <c>terminationDate</c> defaults to today.</summary>
public sealed record TerminateEmployeeRequestDto(DateOnly? TerminationDate, string? Reason);

public sealed record EmployeeDto(
    string Id,
    string FullName,
    string Title,
    string Department,
    DateOnly HiredAt,
    decimal Salary,
    EmployeeStatus Status,
    DateOnly? VacationStartDate,
    DateOnly? VacationEndDate,
    LeaveType? LeaveType,
    string? VacationNote,
    DateOnly? TerminatedAt,
    string? TerminationReason,
    string? TerminationOrderNumber,
    string? StoreId)
{
    public static readonly Expression<Func<Employee, EmployeeDto>> Projection = e => new EmployeeDto(
        e.Id, e.FullName, e.Title, e.Department, e.HiredAt, e.Salary, e.Status,
        e.VacationStartDate, e.VacationEndDate, e.LeaveType, e.VacationNote,
        e.TerminatedAt, e.TerminationReason, e.TerminationOrderNumber, e.StoreId);

    // Compiled once: the same mapping for an entity already in memory.
    private static readonly Func<Employee, EmployeeDto> Map = Projection.Compile();

    public static EmployeeDto From(Employee e) => Map(e);
}

/// <summary>POST /api/employees and its /vacation, /return and /terminate actions → { "ok": true, "employee": { ... } }</summary>
public sealed record EmployeeResponseDto(EmployeeDto Employee) : SuccessResponse;

/// <summary>What the termination order (işdən çıxma əmri) prints.</summary>
public sealed record TerminationDocumentDto(
    string OrderNumber,
    string CompanyName,
    string? CompanyVoen,
    string EmployeeName,
    string Title,
    string Department,
    DateOnly HiredAt,
    DateOnly TerminatedAt,
    string Reason);

/// <summary>GET /api/employees/{id}/termination-document → { "ok": true, "document": { ... } }</summary>
public sealed record TerminationDocumentResponseDto(TerminationDocumentDto Document) : SuccessResponse;
