using System;
using System.Diagnostics;
using System.Reflection;
using CommandLine;
using Dapper;
using tesselate_building_core;
using Wkx;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;

public class Application
{

    static string password = string.Empty;
    static int outputProjection = 4978;

    static string idColumn = "auto_inc_id";

    public static void run(ParserResult<tesselate_building_sample_console.Options> parser)
    {
        
        // Logging logic
        var builder = WebApplication.CreateBuilder();
        builder.Logging.AddConsole();
        var app = builder.Build();

        // Some tooling 
        var version = Assembly.GetEntryAssembly().GetName().Version;
        app.Logger.LogInformation($"Tool: Tesselate buildings {version}");
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        parser.WithParsed(o =>
        {
            // Retrieve username and database from env if not given.
            o.User = string.IsNullOrEmpty(o.User) ? Environment.UserName : o.User;
            o.Database = string.IsNullOrEmpty(o.Database) ? Environment.UserName : o.Database;
            // Create connection with database
            SQLHandler handler = new SQLHandler(o.User, o.Port, o.Host, o.Database, o.Table);
            handler.Connect();
            
            dynamic rowCountDapper = handler.QuerySingle($"select count(*) from {o.Table}");
            var rowCount = Convert.ToDouble(rowCountDapper.count);

            // Retrieve and save current primary key
            var pkeySql = @$"SELECT a.attname FROM  pg_index i 
                            JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey) 
                            WHERE  i.indrelid = '{o.Table}'::regclass AND i.indisprimary;";
            var primaryKey = Convert.ToString(handler.QuerySingle(pkeySql).attname);
            
            // Remove primary key constraint and set it to auto increment table
            var pkConstraint = o.Table.Substring(o.Table.LastIndexOf('.') + 1) + "_pkey";
            app.Logger.LogInformation("Adding auto increment primary key for performance purpuses.");
            handler.CreateBatch();
            handler.AddBatchCommand($"ALTER TABLE {o.Table} DROP CONSTRAINT {pkConstraint}");
            handler.AddBatchCommand($"alter table {o.Table} drop column if exists {idColumn} cascade");
            handler.AddBatchCommand($"ALTER TABLE {o.Table} ADD COLUMN {idColumn} SERIAL PRIMARY KEY;");
            handler.ExecuteBatchCommand();
 

            // Make sure geometry column contains 1 type of geometry. 
            dynamic numGeometryTypes = handler.QuerySingle(
                $"select count(distinct st_geometrytype({o.InputGeometryColumn})) from {o.Table};"
            );

            if (Convert.ToInt32(numGeometryTypes.Count) > 1)
            {
                app.Logger.LogError($@"Found more than 1 geometry type in column: {o.InputGeometryColumn}, 
                                    make sure only 1 geometry type is present. Exiting program.");
            }

            // // Query single geometry for determining type
            var singularGeomSql = @$"select ST_AsEWKT({o.InputGeometryColumn}) as geometry, 
                                        ST_NDims({o.InputGeometryColumn}) as dimensions, 
                                        {idColumn} as id from {o.Table} LIMIT 1;";
            dynamic singularGeom = handler.QuerySingle(singularGeomSql);
            
            
            Wkx.Geometry inputGeometry = Wkx.Geometry.Deserialize<EwktSerializer>(singularGeom.geometry);


            // Determine correct geometry type. 
            Converter converter;
            if (singularGeom.dimensions == 3 && inputGeometry is MultiPolygon) // MultiPolygonZ
            {
                app.Logger.LogInformation("Found 3D Multipolygon.");
                converter = new MultiPolygonConverter();
            }
            else if (singularGeom.dimensions == 3 && inputGeometry is PolyhedralSurface) // PolyhedralSurfaceZ
            {
                app.Logger.LogInformation("Found 3D PolyhedralSurface.");
                converter = new PolyhedralConverter();
            }
            else if (singularGeom.dimensions == 2 && inputGeometry is Polygon) // Polygon
            {
                app.Logger.LogInformation("Found 2D Polygon, making sure they are valid...");
                handler.ExecuteNonQuery(@$"update {o.Table} set {o.InputGeometryColumn} = 
                                        ST_MakeValid({o.InputGeometryColumn})"
                                        );
                converter = new PolygonConverter();
            }
            else
            {
                app.Logger.LogError(@$"No Polygon, MultiPolygonZ or PolyhedralSurfaceZ geometry found in column: 
                                    {o.InputGeometryColumn}, exiting program.");
                converter = null;
                Environment.Exit(0);
            };


            // Add output column
            var outputGeometryColumn = o.InputGeometryColumn + "_3d_triangle";
            app.Logger.LogInformation($"Adding output column...");
            handler.ExecuteNonQuery($"alter table {o.Table} drop column if exists {outputGeometryColumn} cascade");
            handler.ExecuteNonQuery($"alter table {o.Table} add column {outputGeometryColumn} geometry;");

            // Query geometries
            app.Logger.LogInformation("Querying geometries...");
            var heightSql = (singularGeom is Polygon ? $"{o.HeightColumn} as height, " : "");
            var completeGeomSql = @$"select ST_AsBinary({o.InputGeometryColumn}) as geometry, 
                                     {heightSql}{idColumn} as id from {o.Table} ORDER BY {idColumn}";


            app.Logger.LogInformation($"Num geometries to tesselate: {rowCount}");
            var chunk = 1000;
            var offset = 5000000;
            var k = 0;
            while(k < Convert.ToInt32(rowCount)) {
                
                var stopWatch = new Stopwatch();
                stopWatch.Start();
                
                var sql = @$"select ST_AsBinary({o.InputGeometryColumn}) as geometry, 
                            {heightSql}{idColumn} as id from {o.Table} WHERE {idColumn} IN (
                                SELECT {idColumn} FROM {o.Table} WHERE {idColumn} >= {offset} and {idColumn} < {offset + chunk}
                            )";
                
                var reader = handler.ExecuteDataReader(sql);
                var parser = reader.GetRowParser<Building>(typeof(Building));
                handler.CreateBatch();
                while (reader.Read()) 
                {   
                    var building = parser(reader);

                    // Convert geometry to traingulated polyhedralsurface
                    var polyhedral = converter.Convert(new Geometry(building.Geometry), building);

                    // Geometry to wkt format
                    var wkt = polyhedral.SerializeString<WktSerializer>();

                    // Update table row sql
                    var updateSql = $@"update {o.Table} set {outputGeometryColumn} = 
                                        ST_Transform(
                                                ST_Force3D(
                                                    St_SetSrid(
                                                        ST_GeomFromText('{wkt}'), 
                                                    {inputGeometry.Srid})
                                                ), 
                                        {outputProjection}) 
                                        where {idColumn}=$1;";
                    handler.AddBatchCommand(updateSql, Convert.ToInt32(building.Id));
                    k++;
                }
                reader.Close();
                handler.batch.ExecuteNonQuery();    

                stopWatch.Stop();
                app.Logger.LogInformation($"Time spent: {stopWatch.ElapsedMilliseconds / 1000} sec");

                offset += chunk;
                app.Logger.LogInformation($"Tesselated and inserted {offset}/{rowCount} geometries.");
            }

            // Add shaders
            app.Logger.LogInformation("Adding shaders...");
            handler.ExecuteNonQuery($"alter table {o.Table} drop column if exists {outputGeometryColumn}_shader cascade;");
            handler.ExecuteNonQuery($"alter table {o.Table} add {outputGeometryColumn}_shader jsonb;");

            var styleSql = @$"    
                update {o.Table} set {outputGeometryColumn}_shader = jsonb_build_object(	
                    'PbrMetallicRoughness', jsonb_build_object(
                        'BaseColors'::text, array_fill('{o.Color}'::text, ARRAY[ST_NumGeometries({outputGeometryColumn})]), 
                    'MetallicRoughness'::text, array_fill('#008000'::text, ARRAY[ST_NumGeometries({outputGeometryColumn})])));
            ";
            handler.ExecuteNonQuery(styleSql);

            // Filter out wrongly tesselated buildings.
            string deleteSql = @$"
                delete from {o.Table} 
                where ST_NumGeometries({outputGeometryColumn}) is null 
                and ST_NumGeometries({outputGeometryColumn}) = 0 
                and ST_Npoints({outputGeometryColumn})::numeric/st_numgeometries({outputGeometryColumn})::numeric != 4; 
            ";
            handler.ExecuteNonQuery(deleteSql);


            // Create index on triangulated geometry column
            var indexSql = $"create index on {o.Table} using gist(st_centroid(st_envelope({outputGeometryColumn})));";
            handler.ExecuteNonQuery(indexSql);

            // Reset primary key
            handler.CreateBatch(); 
            handler.AddBatchCommand($"ALTER TABLE {o.Table} DROP CONSTRAINT {pkConstraint};");
            handler.AddBatchCommand($"ALTER TABLE {o.Table} ADD CONSTRAINT {pkConstraint} PRIMARY KEY ({primaryKey});");
            handler.AddBatchCommand($"ALTER TABLE {o.Table} DROP COLUMN {idColumn} CASCADE;");
            handler.ExecuteBatchCommand();

            // Close connection
            handler.Close();

            stopWatch.Stop();
            Console.WriteLine();
            app.Logger.LogInformation($"Elapsed: {stopWatch.ElapsedMilliseconds / 1000} seconds");
            app.Logger.LogInformation("Program finished.");
        });
    }
}