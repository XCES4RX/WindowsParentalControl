using Microsoft.Data.Sqlite;

namespace ParentalControl.Core.Data;

public sealed record ActivityItem(string Name, string Title, int DurationSeconds);

public static class ActivityRepository
{
    public static void RecordActivity(int userId, DateOnly date, string activityType, string name, string title, int seconds)
    {
        if (string.IsNullOrWhiteSpace(name) || seconds <= 0) return;

        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO activity_log (user_id, date, activity_type, name, title, duration_seconds)
            VALUES (@userId, @date, @type, @name, @title, @seconds)
            ON CONFLICT(user_id, date, activity_type, name) DO UPDATE SET
                duration_seconds = duration_seconds + excluded.duration_seconds,
                title = CASE WHEN excluded.title != '' THEN excluded.title ELSE title END;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@type", activityType);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@seconds", seconds);
        cmd.ExecuteNonQuery();
    }

    public static List<ActivityItem> GetTopApps(int userId, DateOnly date, int limit = 5)
    {
        return GetTop(userId, date, "APP", limit);
    }

    public static List<ActivityItem> GetTopSites(int userId, DateOnly date, int limit = 5)
    {
        return GetTop(userId, date, "SITE", limit);
    }

    private static List<ActivityItem> GetTop(int userId, DateOnly date, string type, int limit)
    {
        var items = new List<ActivityItem>();
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT name, title, duration_seconds
            FROM activity_log
            WHERE user_id = @userId AND date = @date AND activity_type = @type
            ORDER BY duration_seconds DESC
            LIMIT @limit;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ActivityItem(
                reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.GetInt32(2)
            ));
        }
        return items;
    }
}
