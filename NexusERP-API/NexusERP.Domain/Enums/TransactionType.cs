namespace NexusERP.Domain.Enums;

public enum TransactionType
{
    /// <summary>Money in (mədaxil): increases the register's balance.</summary>
    Income = 1,

    /// <summary>Money out (məxaric): decreases the register's balance.</summary>
    Expense = 2,

    /// <summary>Moves money from one register to another; the total across registers is unchanged.</summary>
    Transfer = 3
}
