using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

/// <summary>A person on the company's staff list (kadr). Not a sign-in account: see <see cref="User"/> for those.</summary>
public class Employee
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Position (vəzifə).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Department (şöbə).</summary>
    public string Department { get; set; } = string.Empty;

    public DateOnly HiredAt { get; set; }

    /// <summary>Monthly gross salary.</summary>
    public decimal Salary { get; set; }

    /// <summary>Store the employee works in; their salary counts toward its profit. Null for office / company-wide staff.</summary>
    public string? StoreId { get; set; }
    public Store? Store { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>The current or most recent leave; kept after the employee returns.</summary>
    public DateOnly? VacationStartDate { get; set; }
    public DateOnly? VacationEndDate { get; set; }
    public LeaveType? LeaveType { get; set; }
    public string? VacationNote { get; set; }

    public DateOnly? TerminatedAt { get; set; }
    public string? TerminationReason { get; set; }

    /// <summary>Number of the termination order (işdən çıxma əmri), per company: ƏMR-001, ƏMR-002, ...</summary>
    public string? TerminationOrderNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
