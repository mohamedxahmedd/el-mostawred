using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SamyrTechWay.SpinWin.Data;

public static class DbErrors
{
    /// <summary>True when the failure is a unique index/constraint violation (SQL Server 2601/2627, SQLite 19/UNIQUE).</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException switch
    {
        SqlException sql => sql.Number is 2601 or 2627,
        DbException db => db.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
