using Microsoft.Data.Sqlite;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

/// <summary>
/// Persistence for per-user Windows policy restrictions.
/// </summary>
public static class UserPolicyRepository
{
    public static UserPolicyConfig? GetByUserId(int userId)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, lock_taskbar, block_settings, block_task_manager, block_cmd
            FROM user_policies
            WHERE user_id = @userId
            """;
        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new UserPolicyConfig
        {
            UserId = reader.GetInt32(0),
            LockTaskbar = reader.GetInt32(1) != 0,
            BlockSettings = reader.GetInt32(2) != 0,
            BlockTaskManager = reader.GetInt32(3) != 0,
            BlockCommandPrompt = reader.GetInt32(4) != 0
        };
    }

    public static void Upsert(UserPolicyConfig config)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO user_policies (user_id, lock_taskbar, block_settings, block_task_manager, block_cmd)
            VALUES (@userId, @lockTaskbar, @blockSettings, @blockTaskManager, @blockCmd)
            ON CONFLICT(user_id) DO UPDATE SET
                lock_taskbar = excluded.lock_taskbar,
                block_settings = excluded.block_settings,
                block_task_manager = excluded.block_task_manager,
                block_cmd = excluded.block_cmd
            """;
        cmd.Parameters.AddWithValue("@userId", config.UserId);
        cmd.Parameters.AddWithValue("@lockTaskbar", config.LockTaskbar ? 1 : 0);
        cmd.Parameters.AddWithValue("@blockSettings", config.BlockSettings ? 1 : 0);
        cmd.Parameters.AddWithValue("@blockTaskManager", config.BlockTaskManager ? 1 : 0);
        cmd.Parameters.AddWithValue("@blockCmd", config.BlockCommandPrompt ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public static void Delete(int userId)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM user_policies WHERE user_id = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.ExecuteNonQuery();
    }
}
