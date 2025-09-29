using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace PortScannerGUI
{
    public class HistoryDB
    {
        private const string DbPath = "history.db";
        private const string LogPath = "audit.log";

        public static void InitializeDatabase()
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT UNIQUE NOT NULL,
                        Password TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS Scans (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Timestamp TEXT NOT NULL,
                        TotalPorts INTEGER NOT NULL,
                        ExportFormat TEXT NOT NULL,
                        ReportPath TEXT,
                        KillNonEssential INTEGER NOT NULL,
                        UserId INTEGER,
                        FOREIGN KEY (UserId) REFERENCES Users(Id)
                    );
                ";
                command.ExecuteNonQuery();

                // Add UserId column if it doesn't exist (for existing DBs)
                try
                {
                    command.CommandText = "ALTER TABLE Scans ADD COLUMN UserId INTEGER;";
                    command.ExecuteNonQuery();
                }
                catch
                {
                    // Column already exists, ignore
                }
            }
        }

        public static void InsertScan(string timestamp, int totalPorts, string? exportFormat, string? reportPath, bool killNonEssential)
        {
            InsertScan(timestamp, totalPorts, exportFormat, reportPath, killNonEssential, null);
        }

        public static List<string> GetAllScans()
        {
            InitializeDatabase();
            var results = new List<string>();
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Timestamp, TotalPorts, ExportFormat, ReportPath, KillNonEssential FROM Scans ORDER BY Timestamp DESC;";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add($"{reader.GetString(0)},{reader.GetInt32(1)},{reader.GetString(2)},{reader.GetString(3)},{reader.GetInt32(4)}");
                    }
                }
            }
            LogAudit("Scans retrieved");
            return results;
        }

        public static void MigrateFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return;
            try
            {
                var json = File.ReadAllText(jsonPath);
                var history = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                if (history != null)
                {
                    foreach (var entry in history)
                    {
                        var timestamp = entry.ContainsKey("Timestamp") ? entry["Timestamp"].ToString() : "";
                        var totalPorts = entry.ContainsKey("TotalPorts") ? Convert.ToInt32(entry["TotalPorts"]) : 0;
                        var exportFormat = entry.ContainsKey("ExportFormat") ? entry["ExportFormat"].ToString() : "TXT";
                        var reportPath = entry.ContainsKey("ReportPath") ? entry["ReportPath"].ToString() : "";
                        var killNonEssential = entry.ContainsKey("KillNonEssential") && (bool)entry["KillNonEssential"];
                        InsertScan(timestamp, totalPorts, exportFormat, reportPath, killNonEssential);
                    }
                }
                LogAudit("Migration from JSON completed");
            }
            catch (Exception ex)
            {
                LogAudit($"Error migrating from JSON: {ex.Message}");
                Console.WriteLine($"Error migrating from JSON: {ex.Message}");
            }
        }

        public static void LogAudit(string message)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging: {ex.Message}");
            }
        }

        public static bool RegisterUser(string username, string password)
        {
            InitializeDatabase();
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Users (Username, Password)
                    VALUES ($username, $password);
                ";
                command.Parameters.AddWithValue("$username", username);
                command.Parameters.AddWithValue("$password", password);
                try
                {
                    command.ExecuteNonQuery();
                    LogAudit($"User registered: {username}");
                    return true;
                }
                catch
                {
                    return false; // Username exists or error
                }
            }
        }

        public static int? LoginUser(string username, string password)
        {
            InitializeDatabase();
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id FROM Users WHERE Username = $username AND Password = $password;";
                command.Parameters.AddWithValue("$username", username);
                command.Parameters.AddWithValue("$password", password);
                var result = command.ExecuteScalar();
                if (result != null)
                {
                    LogAudit($"User logged in: {username}");
                    return Convert.ToInt32(result);
                }
                return null;
            }
        }

        public static void InsertScan(string timestamp, int totalPorts, string? exportFormat, string? reportPath, bool killNonEssential, int? userId)
        {
            InitializeDatabase();
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Scans (Timestamp, TotalPorts, ExportFormat, ReportPath, KillNonEssential, UserId)
                    VALUES ($timestamp, $totalPorts, $exportFormat, $reportPath, $killNonEssential, $userId);
                ";
                command.Parameters.AddWithValue("$timestamp", timestamp);
                command.Parameters.AddWithValue("$totalPorts", totalPorts);
                command.Parameters.AddWithValue("$exportFormat", exportFormat ?? "TXT");
                command.Parameters.AddWithValue("$reportPath", reportPath ?? "");
                command.Parameters.AddWithValue("$killNonEssential", killNonEssential ? 1 : 0);
                command.Parameters.AddWithValue("$userId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                command.ExecuteNonQuery();
            }
        }
    }
}
