using System;
using System.Diagnostics;
using System.Reflection;
using CommandLine;
using Dapper;
using tesselate_building_core;
using Wkx;

public class Application
{

    static string password = string.Empty;
    static int outputProjection = 4978;

    public static void run(ParserResult<tesselate_building_sample_console.Options> parser)
    {

        // Some tooling 
        var version = Assembly.GetEntryAssembly().GetName().Version;
        Console.WriteLine($"Tool: Tesselate buildings {version}");
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

            // Make sure geometry column contains 1 type of geometry. // TODO CHANGE TO Scalar 
            dynamic numGeometryTypes = handler.QuerySingle($"select count(distinct st_geometrytype({o.InputGeometryColumn})) from {o.Table};");
            if (Convert.ToInt32(numGeometryTypes.Count) > 1)
            {
                Console.WriteLine($@"Found more than 1 geometry type in column: {o.InputGeometryColumn}, make sure only 1 geometry type is present. 
                                        Exiting program.");
            }


            // // Query single geometry for determining type
            var singularGeomSql = @$"select ST_AsEWKT({o.InputGeometryColumn}) as geometry, 
                                        ST_NDims({o.InputGeometryColumn}) as dimensions, 
                                        {o.IdColumn} as id from {o.Table} LIMIT 1;";
            dynamic singularGeom = handler.QuerySingle(singularGeomSql);
            Wkx.Geometry inputGeometry = Wkx.Geometry.Deserialize<EwktSerializer>(singularGeom.geometry);


            // Determine correct geometry type. 
            Converter converter;
            if (singularGeom.dimensions == 3 && inputGeometry is MultiPolygon) // MultiPolygonZ
            {
                Console.WriteLine("Found 3D Multipolygon.");
                converter = new MultiPolygonConverter();
            }
            else if (singularGeom.dimensions == 3 && inputGeometry is PolyhedralSurface) // PolyhedralSurfaceZ
            {
                Console.WriteLine("Found 3D PolyhedralSurface.");
                converter = new PolyhedralConverter();
            }
            else if (singularGeom.dimensions == 2 && inputGeometry is Polygon) // Polygon
            {
                Console.WriteLine("Found 2D Polygon, making sure they are valid...");
                handler.ExecuteNonQuery($"update {o.Table} set {o.InputGeometryColumn} = ST_MakeValid({o.InputGeometryColumn})");
                converter = new PolygonConverter();
            }
            else
            {
                Console.WriteLine($"No Polygon, MultiPolygonZ or PolyhedralSurfaceZ geometry found in column: {o.InputGeometryColumn}, exiting program.");
                converter = null;
                Environment.Exit(0);
            };


            // Add output column
            var outputGeometryColumn = o.InputGeometryColumn + "_3d_triangle";
            Console.WriteLine($"Adding output column.");
            handler.ExecuteNonQuery($"alter table {o.Table} drop column if exists {outputGeometryColumn} cascade");
            handler.ExecuteNonQuery($"alter table {o.Table} add column {outputGeometryColumn} geometry;");

            // Query geometries
            var heightSql = (singularGeom is Polygon ? $"{o.HeightColumn} as height, " : "");
            var completeGeomSql = $"select ST_AsBinary({o.InputGeometryColumn}) as geometry, {heightSql}{o.IdColumn} as id from {o.Table}";
            var buildings = handler.Query<Building>(completeGeomSql);
            Console.WriteLine("Tesselating geometries...");

            // Create batch commmand
            handler.CreateBatch();

            var i = 1;
            foreach (var building in buildings)
            {

                // Convert geometry to traingulated polyhedralsurface
                var polyhedral = converter.Convert(new Geometry(building.Geometry), building).Geom;

                // Geometry to wkt format
                var wkt = polyhedral.SerializeString<WktSerializer>();

                // Update table row sql
                var updateSql = $@"update {o.Table} set {outputGeometryColumn} = 
                                    ST_Transform(ST_Force3D(St_SetSrid(ST_GeomFromText('{wkt}'), $1)), $2) 
                                    where {o.IdColumn}=$3;";
                handler.AddBatchCommand(updateSql, inputGeometry.Srid, outputProjection, Convert.ToInt32(building.Id));

                // Progress bar logic
                var perc = Math.Round((double)i / buildings.AsList().Count * 100, 2);
                Console.Write($"\rProgress: {perc.ToString("F")}%");
                i++;
            }

            // Execute batched query
            Console.WriteLine();
            Console.WriteLine("Writing to database...");
            handler.ExecuteBatchCommand();

            // Add shaders
            Console.WriteLine("Adding shaders...");
            handler.ExecuteNonQuery($"alter table {o.Table} drop column if exists {outputGeometryColumn}_shader cascade;");
            handler.ExecuteNonQuery($"alter table {o.Table} add {outputGeometryColumn}_shader jsonb;");

            var styleSql = @$"    
                update {o.Table} set {outputGeometryColumn}_shader = jsonb_build_object(	
                    'PbrMetallicRoughness', jsonb_build_object(
                        'BaseColors'::text, array_fill('@color'::text, ARRAY[ST_NumGeometries({outputGeometryColumn})]), 
                    'MetallicRoughness'::text, array_fill('#008000'::text, ARRAY[ST_NumGeometries({outputGeometryColumn})])));
            ";
            handler.ExecuteNonQuery(styleSql, o.Color);

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

            // Close connection
            handler.Close();

            stopWatch.Stop();
            Console.WriteLine();
            Console.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds / 1000} seconds");
            Console.WriteLine("Program finished.");
        });
    }
}