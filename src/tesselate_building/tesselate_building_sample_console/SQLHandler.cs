using System;
using System.Collections.Generic;
using Dapper;
using Npgsql;
using tesselate_building_sample_console;

public class SQLHandler
{

    private string connectionString { get; set; }

    private string password { get; set; }

    private string user { get; set; }

    private string port { get; set; }

    private string host { get; set; }

    private string database { get; set; }

    private string table { get; set; }

    public NpgsqlConnection conn { get; set; }

    public NpgsqlBatch batch {get; set;}

    public SQLHandler(string user, string port, string host, string database, string table)
    {
        this.user = user;
        this.port = port;
        this.host = host;
        this.database = database;
        this.table = table;
    }

    public void Connect()
    {
        this.connectionString = @$"
                                Host={this.host};Username={this.user};
                                Database={this.database};Port={this.port};
                                Write Buffer Size=12000; Read Buffer Size=12000;
                                CommandTimeOut=100000";

        var istrusted = TrustedConnectionChecker.HasTrustedConnection(this.connectionString);

        if (!istrusted)
        {
            Console.Write($"Password for user {this.user}: ");
            this.password = PasswordAsker.GetPassword();
            this.connectionString += $";password={password}";
            Console.WriteLine();
        }

        this.conn = new NpgsqlConnection(this.connectionString);
        this.Open();
        SqlMapper.AddTypeHandler(new GeometryTypeHandler());
    }

    public void ExecuteNonQuery(string sql, params object[] parameters)
    {
        NpgsqlCommand command;

        if (parameters.Length > 0)
        {
            command = new NpgsqlCommand(sql, this.conn);

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param);
            }

            command.Prepare();
        }
        else
        {
            command = new NpgsqlCommand(sql, this.conn);
        }

        command.ExecuteNonQuery();
    }

    public NpgsqlDataReader ExecuteDataReader(string sql, params object[] parameters) 
    {
        NpgsqlCommand command = new NpgsqlCommand(sql, this.conn);

        if (parameters.Length > 0)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param);
                command.Prepare();
            }
        }
 

        return command.ExecuteReader();
    }

    public dynamic QuerySingle(string sql)
    {
        return this.conn.QuerySingle(sql);
    }

    public IEnumerable<T> Query<T>(string sql) where T : new ()
    {
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
