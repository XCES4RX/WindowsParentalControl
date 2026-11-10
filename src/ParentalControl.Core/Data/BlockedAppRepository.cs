using ParentalControl.Core.Data;

namespace ParentalControl.Core.Data;

/// <summary>
/// Persistence for per-user blocked application process names.
/// </summary>
public static class BlockedAppRepository
{
    public static List<string> GetByUserId(int userId)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT process_name FROM blocked_apps WHERE user_id = @userId ORDER BY process_name";
        cmd.Parameters.AddWithValue("@userId", userId);

        var apps = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            apps.Add(reader.GetString(0));
        }
        return apps;
    }

    public static void ReplaceAll(int userId, IEnumerable<string> processNames)
    {
        var normalized = processNames
            .Select(NormalizeProcessName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var connection = DatabaseManager.CreateConnection();
        using var transaction = connection.BeginTransaction();

        using (var delCmd = connection.CreateCommand())
        {
            delCmd.Transaction = transaction;
            delCmd.CommandText = "DELETE FROM blocked_apps WHERE user_id = @userId";
            delCmd.Parameters.AddWithValue("@userId", userId);
            delCmd.ExecuteNonQuery();
        }

        using (var insCmd = connection.CreateCommand())
        {
            insCmd.Transaction = transaction;
            insCmd.CommandText = "INSERT INTO blocked_apps (user_id, process_name) VALUES (@userId, @name)";
            insCmd.Parameters.AddWithValue("@userId", userId);
            var nameParam = insCmd.Parameters.Add("@name", Microsoft.Data.Sqlite.SqliteType.Text);

            foreach (var name in normalized)
            {
                nameParam.Value = name;
                insCmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public static bool Add(int userId, string processName)
    {
        var norm = NormalizeProcessName(processName);
        if (string.IsNullOrEmpty(norm)) return false;

        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO blocked_apps (user_id, process_name)
            VALUES (@userId, @name)
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@name", norm);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool Remove(int userId, string processName)
    {
        var norm = NormalizeProcessName(processName);
        if (string.IsNullOrEmpty(norm)) return false;

        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM blocked_apps WHERE user_id = @userId AND process_name = @name";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@name", norm);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static string NormalizeProcessName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var trimmed = raw.Trim().Trim('"', '\'');
        var fileName = Path.GetFileName(trimmed);
        if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^4];
        }
        return fileName.Trim().ToLowerInvariant();
    }
}
