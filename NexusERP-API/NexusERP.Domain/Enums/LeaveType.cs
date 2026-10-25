namespace NexusERP.Domain.Enums;

/// <summary>Kind of leave (məzuniyyət növü).</summary>
public enum LeaveType
{
    /// <summary>İllik (əmək) məzuniyyəti.</summary>
    Annual = 1,

    /// <summary>Ödənişsiz məzuniyyət.</summary>
    Unpaid = 2,

    /// <summary>Təhsil məzuniyyəti.</summary>
    Study = 3,

    /// <summary>Sosial (analıq, uşağa qulluq) məzuniyyəti.</summary>
    Social = 4,

    Other = 5
}
