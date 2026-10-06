using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace SiemensHmi
{
    public class HistoryRecord
    {
        public int Id { get; set; }
        public DateTime RecordTime { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public long ProductionCount { get; set; }
    }

    public class DatabaseService
    {
        private readonly string _dbPath = "Data Source=hmi_history.db";

        public void Initialize()
        {
            using var connection = new SqliteConnection(_dbPath);
            connection.Open();
            const string sql = @"create table if not exists history(
                id integer primary key autoincrement,
                recordtime text not null,
                temperature real not null,
                pressure real not null,
                productioncount integer not null)";
            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        public void InsertRecord(double temp, double pressure, long count)
        {
            using var connection = new SqliteConnection(_dbPath);
            connection.Open();
            const string sql = "insert into history(RecordTime, Temperature, Pressure, ProductionCount) values (@time, @temp, @press, @count);";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@temp", temp);
            command.Parameters.AddWithValue("@press", pressure);
            command.Parameters.AddWithValue("@count", count);
            command.ExecuteNonQuery();
        }

        public List<HistoryRecord> GetLatestRecords(int limit = 100)
        {
            var list = new List<HistoryRecord>();
            using var connection = new SqliteConnection(_dbPath);
            connection.Open();
            string sql = $"SELECT * FROM History ORDER BY Id DESC LIMIT {limit};";
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new HistoryRecord
                {
                    Id = reader.GetInt32(0),
                    RecordTime = DateTime.Parse(reader.GetString(1)),
                    Temperature = reader.GetDouble(2),
                    Pressure = reader.GetDouble(3),
                    ProductionCount = reader.GetInt64(4)
                });
            }
            return list;
        }
    }
}
