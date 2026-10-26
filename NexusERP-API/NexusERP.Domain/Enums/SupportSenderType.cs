namespace NexusERP.Domain.Enums;

public enum SupportSenderType
{
    /// <summary>The company user who opened the ticket.</summary>
    User = 1,

    /// <summary>Automatic reply (greeting on a new ticket, note on resolve); no sending user.</summary>
    SystemBot = 2,

    /// <summary>A platform administrator.</summary>
    SuperAdmin = 3
}
