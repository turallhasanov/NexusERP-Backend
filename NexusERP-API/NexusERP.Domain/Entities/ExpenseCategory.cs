namespace NexusERP.Domain.Entities;

/// <summary>Expense category (xərc maddəsi): rent, utilities, salaries, ...</summary>
public class ExpenseCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
