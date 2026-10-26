using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Employees;

public class EmployeeService(IApplicationDbContext db, IAuditLogger audit) : IEmployeeService
{
    private const string OrderNumberPrefix = "ƏMR";

    // Must match the column definitions in EmployeeConfiguration (salary is decimal(18,2)).
    private const int FullNameMaxLength = 200;
    private const int TitleMaxLength = 100;
    private const int DepartmentMaxLength = 100;
    private const int NoteMaxLength = 500;
    private const decimal MaxSalary = 9_999_999_999_999_999.99m;

    private const string EmployeeNotFound = "İşçi tapılmadı.";
    private const string AlreadyTerminated = "İşçi artıq işdən çıxarılıb.";
    private const string StoreNotFound = "Mağaza tapılmadı.";

    // Leave and termination dates are Azerbaijani calendar days: UTC+4 all year (no daylight saving since 2016).
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4));

    public async Task<Result<ItemsResponseDto<EmployeeDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        // A leave that ended before today is over: those employees are back at work.
        var today = Today;
        await db.Employees
            .Where(e => e.CompanyId == companyId && e.Status == EmployeeStatus.OnLeave && e.VacationEndDate < today)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EmployeeStatus.Active), cancellationToken);

        var items = await db.Employees
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(EmployeeDto.Projection)
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<EmployeeDto>(items);
    }

    public async Task<Result<EmployeeResponseDto>> CreateAsync(string companyId, CreateEmployeeRequestDto request, CancellationToken cancellationToken)
    {
        var profile = ProfileFields.From(request.FullName, request.Title, request.Department, request.HiredAt, request.Salary, request.StoreId);

        if (profile.Error is not null)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, profile.Error);

        if (profile.StoreId is not null && !await IsActiveStoreAsync(companyId, profile.StoreId, cancellationToken))
            return Result<EmployeeResponseDto>.Failure(ErrorType.NotFound, StoreNotFound);

        var employee = new Employee { CompanyId = companyId };
        profile.ApplyTo(employee);

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(companyId, AuditModule.HR, AuditAction.Create,
            $"'{employee.FullName}' işçisi işə qəbul edildi ({employee.Title}, {employee.Department})", cancellationToken);

        return new EmployeeResponseDto(EmployeeDto.From(employee));
    }

    /// <summary>
    /// Corrects the profile (name, position, department, hire date, salary, store). Status, leave and termination
    /// are changed by their own actions only.
    /// </summary>
    public async Task<Result<EmployeeResponseDto>> UpdateAsync(string companyId, string id, UpdateEmployeeRequestDto request, CancellationToken cancellationToken)
    {
        var profile = ProfileFields.From(request.FullName, request.Title, request.Department, request.HiredAt, request.Salary, request.StoreId);

        if (profile.Error is not null)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, profile.Error);

        var employee = await FindAsync(companyId, id, cancellationToken);

        if (employee is null)
            return Result<EmployeeResponseDto>.Failure(ErrorType.NotFound, EmployeeNotFound);

        // A store closed since keeps its staff, but nobody is moved to a closed store.
        if (profile.StoreId is not null && profile.StoreId != employee.StoreId && !await IsActiveStoreAsync(companyId, profile.StoreId, cancellationToken))
            return Result<EmployeeResponseDto>.Failure(ErrorType.NotFound, StoreNotFound);

        if (employee.TerminatedAt < profile.HiredAt)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, "İşə qəbul tarixi işdən çıxma tarixindən sonra ola bilməz.");

        if (employee.VacationStartDate < profile.HiredAt)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, "İşə qəbul tarixi məzuniyyətin başlanğıcından sonra ola bilməz.");

        profile.ApplyTo(employee);
        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(companyId, AuditModule.HR, AuditAction.Update,
            $"'{employee.FullName}' işçisinin məlumatları yeniləndi", cancellationToken);

        return new EmployeeResponseDto(EmployeeDto.From(employee));
    }

    /// <summary>Records the leave and marks the employee as on leave; a leave in progress can be changed this way.</summary>
    public async Task<Result<EmployeeResponseDto>> SendOnVacationAsync(string companyId, string id, VacationRequestDto request, CancellationToken cancellationToken)
    {
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var leaveType = request.LeaveType ?? LeaveType.Annual;

        var error =
            request.StartDate is not { } start ? "Məzuniyyətin başlanğıc tarixi tələb olunur."
            : request.EndDate is not { } end ? "Məzuniyyətin bitmə tarixi tələb olunur."
            : end < start ? "Bitmə tarixi başlanğıc tarixindən əvvəl ola bilməz."
            : !Enum.IsDefined(leaveType) ? "Məzuniyyət növü yanlışdır."
            : note?.Length > NoteMaxLength ? $"Qeyd {NoteMaxLength} simvoldan uzun ola bilməz."
            : null;

        if (error is not null)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, error);

        var employee = await FindAsync(companyId, id, cancellationToken);

        if (employee is null)
            return Result<EmployeeResponseDto>.Failure(ErrorType.NotFound, EmployeeNotFound);

        if (employee.Status == EmployeeStatus.Terminated)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Conflict, AlreadyTerminated);

        if (request.StartDate < employee.HiredAt)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, "Məzuniyyət işə qəbul tarixindən əvvəl başlaya bilməz.");

        if (request.EndDate < Today)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, "Bitmiş məzuniyyəti qeyd etmək olmaz: bitmə tarixi bu gün və ya sonra olmalıdır.");

        employee.Status = EmployeeStatus.OnLeave;
        employee.VacationStartDate = request.StartDate;
        employee.VacationEndDate = request.EndDate;
        employee.LeaveType = leaveType;
        employee.VacationNote = note;
        await db.SaveChangesAsync(cancellationToken);

        return new EmployeeResponseDto(EmployeeDto.From(employee));
    }

    /// <summary>"İşə qayıtdı": ends the leave early (or confirms its end); the leave's dates stay on record.</summary>
    public async Task<Result<EmployeeResponseDto>> ReturnFromVacationAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var employee = await FindAsync(companyId, id, cancellationToken);

        if (employee is null)
            return Result<EmployeeResponseDto>.Failure(ErrorType.NotFound, EmployeeNotFound);

        if (employee.Status != EmployeeStatus.OnLeave)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Conflict, "İşçi məzuniyyətdə deyil.");

        employee.Status = EmployeeStatus.Active;
        await db.SaveChangesAsync(cancellationToken);

        return new EmployeeResponseDto(EmployeeDto.From(employee));
    }

    /// <summary>Ends employment and numbers the termination order (ƏMR-001, ...), which can then be printed.</summary>
    public async Task<Result<EmployeeResponseDto>> TerminateAsync(string companyId, string id, TerminateEmployeeRequestDto request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        var date = request.TerminationDate ?? Today;

        if (string.IsNullOrEmpty(reason))
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, "İşdən çıxma səbəbi tələb olunur.");

        if (reason.Length > NoteMaxLength)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, $"Səbəb {NoteMaxLength} simvoldan uzun ola bilməz.");

        var employee = await FindAsync(companyId, id, cancellationToken);

        if (employee is null)
            return Result<EmployeeResponseDto>.Failure(ErrorType.NotFound, EmployeeNotFound);

        if (employee.Status == EmployeeStatus.Terminated)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Conflict, AlreadyTerminated);

        if (date < employee.HiredAt)
            return Result<EmployeeResponseDto>.Failure(ErrorType.Validation, "İşdən çıxma tarixi işə qəbul tarixindən əvvəl ola bilməz.");

        employee.Status = EmployeeStatus.Terminated;
        employee.TerminatedAt = date;
        employee.TerminationReason = reason;

        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Employees.Where(e => e.CompanyId == companyId && e.TerminationOrderNumber != null).Select(e => e.TerminationOrderNumber!),
            OrderNumberPrefix,
            number => employee.TerminationOrderNumber = number,
            cancellationToken);

        await audit.LogAsync(companyId, AuditModule.HR, AuditAction.Update,
            $"'{employee.FullName}' işçisi işdən çıxarıldı (əmr {employee.TerminationOrderNumber}, səbəb: {reason})", cancellationToken);

        return new EmployeeResponseDto(EmployeeDto.From(employee));
    }

    public async Task<Result<TerminationDocumentResponseDto>> GetTerminationDocumentAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var document = await db.Employees
            .AsNoTracking()
            .Where(e => e.Id == id && e.CompanyId == companyId)
            .Select(e => new
            {
                e.Status,
                Document = new TerminationDocumentDto(
                    e.TerminationOrderNumber ?? string.Empty,
                    db.Companies.Where(c => c.Id == e.CompanyId).Select(c => c.Name).First(),
                    db.Companies.Where(c => c.Id == e.CompanyId).Select(c => c.Voen).First(),
                    e.FullName,
                    e.Title,
                    e.Department,
                    e.HiredAt,
                    e.TerminatedAt ?? e.HiredAt,
                    e.TerminationReason ?? string.Empty)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
            return Result<TerminationDocumentResponseDto>.Failure(ErrorType.NotFound, EmployeeNotFound);

        if (document.Status != EmployeeStatus.Terminated)
            return Result<TerminationDocumentResponseDto>.Failure(ErrorType.NotFound, "Bu işçi üçün işdən çıxma əmri yoxdur.");

        return new TerminationDocumentResponseDto(document.Document);
    }

    /// <summary>
    /// Removes the employee for good, with their leave and termination order; payroll reports stop counting them,
    /// past periods included. Nothing else references an employee, so there is nothing to block it.
    /// </summary>
    public async Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var name = await db.Employees
            .Where(e => e.Id == id && e.CompanyId == companyId)
            .Select(e => e.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var deleted = name is null
            ? 0
            : await db.Employees
                .Where(e => e.Id == id && e.CompanyId == companyId)
                .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
            return Result<SuccessResponse>.Failure(ErrorType.NotFound, EmployeeNotFound);

        await audit.LogAsync(companyId, AuditModule.HR, AuditAction.Delete, $"'{name}' işçisi sistemdən silindi", cancellationToken);

        return new SuccessResponse();
    }

    private Task<Employee?> FindAsync(string companyId, string id, CancellationToken cancellationToken) =>
        db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

    private Task<bool> IsActiveStoreAsync(string companyId, string storeId, CancellationToken cancellationToken) =>
        db.Stores.AnyAsync(s => s.Id == storeId && s.CompanyId == companyId && s.IsActive, cancellationToken);

    /// <summary>Trimmed, validated profile fields shared by create and update; <c>Error</c> is set when they are not valid.</summary>
    private sealed record ProfileFields(string FullName, string Title, string Department, DateOnly HiredAt, decimal Salary, string? StoreId, string? Error)
    {
        public static ProfileFields From(string? fullName, string? title, string? department, DateOnly? hiredAt, decimal? salary, string? storeId)
        {
            var name = fullName?.Trim() ?? string.Empty;
            var position = title?.Trim() ?? string.Empty;
            var unit = department?.Trim() ?? string.Empty;

            var error =
                name.Length == 0 ? "Ad və soyad tələb olunur."
                : name.Length > FullNameMaxLength ? $"Ad {FullNameMaxLength} simvoldan uzun ola bilməz."
                : position.Length == 0 ? "Vəzifə tələb olunur."
                : position.Length > TitleMaxLength ? $"Vəzifə {TitleMaxLength} simvoldan uzun ola bilməz."
                : unit.Length == 0 ? "Şöbə seçilməlidir."
                : unit.Length > DepartmentMaxLength ? $"Şöbə {DepartmentMaxLength} simvoldan uzun ola bilməz."
                : hiredAt is null ? "İşə qəbul tarixi tələb olunur."
                : salary is not { } amount || amount <= 0 ? "Maaş 0-dan böyük olmalıdır."
                : amount > MaxSalary ? "Maaş həddindən artıq böyükdür."
                : null;

            return new ProfileFields(
                name,
                position,
                unit,
                hiredAt ?? default,
                Math.Round(salary ?? 0, 2),
                string.IsNullOrWhiteSpace(storeId) ? null : storeId.Trim(),
                error);
        }

        public void ApplyTo(Employee employee)
        {
            employee.FullName = FullName;
            employee.Title = Title;
            employee.Department = Department;
            employee.HiredAt = HiredAt;
            employee.Salary = Salary;
            employee.StoreId = StoreId;
        }
    }
}
