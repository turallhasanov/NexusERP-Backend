namespace NexusERP.Domain.Enums;

public enum EmployeeStatus
{
    Active = 1,

    /// <summary>On leave (məzuniyyətdə) from VacationStartDate to VacationEndDate; back to Active after the end date.</summary>
    OnLeave = 2,

    /// <summary>Employment ended (işdən çıxıb) on TerminatedAt; final.</summary>
    Terminated = 3
}
