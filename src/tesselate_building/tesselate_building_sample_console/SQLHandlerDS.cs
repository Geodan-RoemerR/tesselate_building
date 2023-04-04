using System;
using System.Collections.Generic;
using Dapper;
using Npgsql;
using tesselate_building_sample_console;

public class SQLHandlerDC
{

    private string connectionString { get; set; }

    private string password { get; set; }

    private string user { get; set; }

    private string port { get; set; }

    private string host { get; set; }

    private string database { get; set; }

    private string table { get; set; }

    public NpgsqlConnection conn { get; set; }

    public NpgsqlDataSource dc { get; set; }

    public NpgsqlBatch batch { get; set; }

    public SQLHandlerDC(string user, string port, string host, string database, string table)
    {
        this.user = user;
        this.port = port;
        this.host = host;
        this.database = database;
        this.table = table;
    }

    public void Connect()
    {
        this.connectionString = $"Host={this.host};Username={this.user};Database={this.database};Port={this.port};CommandTimeOut=300";

        var istrusted = TrustedConnectionChecker.HasTrustedConnection(this.connectionString);

        if (!istrusted)
        {
            Console.Write($"Password for user {this.user}: ");
            this.password = PasswordAsker.GetPassword();
            this.connectionString += $";password={password}";
            Console.WriteLine();
        }

        this.dc = NpgsqlDataSource.Create(this.connectionString);

        // this.conn = new NpgsqlConnection(this.connectionString);
        // this.Open();
        SqlMapper.AddTypeHandler(new GeometryTypeHandler());
    }

    public void ExecuteNonQuery(string sql, params object[] parameters)
    {
        NpgsqlCommand command = this.dc.CreateCommand(sql);

        if (parameters.Length > 0)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param);
            }

            command.Prepare();
        }

        command.ExecuteNonQuery();
    }

    public dynamic QuerySingle(string sql)
    {
        NpgsqlCommand command = this.dc.CreateCommand(sql);
        return command.ExecuteScalar();;
    }

    public IEnumerable<T> Query<T>(string sql) where T : new ()
    {
        NpgsqlCommand command = this.dc.CreateCommand(sql);
        // command.
        return this.conn.Query<T>(sql);
    }

    public void CreateBatch() {
        this.batch = new NpgsqlBatch(this.conn);
    }

    public void AddBatchCommand(string sql, params object[] parameters) {
        NpgsqlBatchCommand command = new NpgsqlBatchCommand(sql);
        
        if (parameters.Length > 0)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param);
            }
        }

        this.batch.BatchCommands.Add(command);
        
    }

    public void ExecuteBatchCommand() {
        this.batch.Prepare();
        this.batch.ExecuteNonQuery();
    }

    public void Open()
    {
        this.conn.Open();
    }

    public void Close()
    {
        this.conn.Close();
    }

}
