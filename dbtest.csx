using Microsoft.Data.Sqlite;
var conn = new SqliteConnection("Data Source=nopasaran.db");
conn.Open();
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Id, Name, IsPlayerControlled FROM Teams";
var reader = cmd.ExecuteReader();
System.Console.WriteLine("Teams in database:");
while(reader.Read()) {
    System.Console.WriteLine($"ID: {reader.GetInt32(0)}, Name: {reader.GetString(1)}, IsPlayer: {reader.GetInt32(2)}");
}
reader.Close();

cmd.CommandText = "SELECT COUNT(*) FROM Matches";
var count = cmd.ExecuteScalar();
System.Console.WriteLine($"\nTotal Matches: {count}");

cmd.CommandText = "SELECT Id, HomeTeamId, AwayTeamId, IsPlayed FROM Matches LIMIT 5";
reader = cmd.ExecuteReader();
System.Console.WriteLine("\nFirst 5 Matches:");
while(reader.Read()) {
    System.Console.WriteLine($"Match {reader.GetInt32(0)}: Team {reader.GetInt32(1)} vs Team {reader.GetInt32(2)}, Played: {reader.GetInt32(3)}");
}
conn.Close();
