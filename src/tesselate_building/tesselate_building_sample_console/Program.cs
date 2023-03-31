using CommandLine;
using Dapper;
using Npgsql;
using System;
using System.Diagnostics;
using System.Reflection;
using tesselate_building_core;
using Wkx;

namespace tesselate_building_sample_console
{
    class Program
    {
        static string password = string.Empty;
        enum InputGeometryType
        {
            PolyhedralSurfaceZ,
            Polygon,
            MultiPolygonZ,
            Undefined
        }
        static void Main(string[] args)
        {
            var version = Assembly.GetEntryAssembly().GetName().Version;
            Console.WriteLine($"Tool: Tesselate buildings {version}");
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
            {
                o.User = string.IsNullOrEmpty(o.User) ? Environment.UserName : o.User;
                o.Database = string.IsNullOrEmpty(o.Database) ? Environment.UserName : o.Database;

                var outputProjection = 4978;
                // (o.Format == "mapbox" ? 3857 : 4978);
                var connectionString = $"Host={o.Host};Username={o.User};Database={o.Database};Port={o.Port}";

                var istrusted = TrustedConnectionChecker.HasTrustedConnection(connectionString);

                if (!istrusted)
                {
                    Console.Write($"Password for user {o.User}: ");
                    password = PasswordAsker.GetPassword();
                    connectionString += $";password={password}";
                    Console.WriteLine();
                }
                var conn = new NpgsqlConnection(connectionString);
                conn.Open();
                SqlMapper.AddTypeHandler(new GeometryTypeHandler());

                // Make sure geometry column contains 1 type of geometry.
                dynamic num_geometries = conn.QuerySingle($"select count(distinct st_geometrytype({o.InputGeometryColumn})) from {o.Table};");
                if (num_geometries > 1)
                {
                    Console.WriteLine($@"Found more than 1 geometry type in column: {o.InputGeometryColumn}, make sure only 1 geometry type is present. 
                                         Exiting program.");
                }

                // Query single geometry for determining type
                var singularGeomSql = @$"select ST_AsEWKT({o.InputGeometryColumn}) as geometry, 
                                            ST_NDims({o.InputGeometryColumn}) as dimensions, 
                                            {o.IdColumn} as id from {o.Table} LIMIT 1;";
                dynamic singularGeom = conn.QuerySingle(singularGeomSql);
                Geometry inputGeometry = Geometry.Deserialize<EwktSerializer>(singularGeom.geometry);

                // Determine correct geometry type.
                InputGeometryType geomType;
                if (singularGeom.dimensions == 3 && inputGeometry is MultiPolygon) // MultiPolygonZ
                {
                    Console.WriteLine("Found 3D Multipolygon.");
                    geomType = InputGeometryType.MultiPolygonZ;

                }
                else if (singularGeom.dimensions == 3 && inputGeometry is PolyhedralSurface) // PolyhedralSurfaceZ
                {
                    Console.WriteLine("Found 3D PolyhedralSurface.");
                    geomType = InputGeometryType.PolyhedralSurfaceZ;

                }
                else if (singularGeom.dimensions == 2 && inputGeometry is Polygon) // Polygon
                {
                    Console.WriteLine("Found 2D Polygon, making sure they are valid...");
                    conn.Execute($"update {o.Table} set {o.InputGeometryColumn} = ST_MakeValid({o.InputGeometryColumn})");
                    geomType = InputGeometryType.Polygon;

                }
                else
                {
                    Console.WriteLine($"No Polygon, MultiPolygonZ or PolyhedralSurfaceZ geometry found in column: {o.InputGeometryColumn}, exiting program.");
                    geomType = InputGeometryType.Undefined;
                    Environment.Exit(0);

                };

                // Add output column
                var outputGeometryColumn = o.InputGeometryColumn + "_3d_triangle";
                Console.WriteLine($"Adding output column.");
                conn.Execute($"alter table {o.Table} drop column if exists {outputGeometryColumn} cascade");
                conn.Execute($"alter table {o.Table} add column {outputGeometryColumn} geometry;");

                // Query geometries
                var heightSql = (geomType == InputGeometryType.Polygon ? $"{o.HeightColumn} as height, " : "");
                var select = $"select ST_AsBinary({o.InputGeometryColumn}) as geometry, {heightSql}{o.IdColumn} as id";
                var sql = $"{select} from {o.Table};";
                var buildings = conn.Query<Building>(sql);

                Console.WriteLine("Tesselating geometries...");

                var i = 1;
                foreach (var building in buildings)
                {
                    var polyhedral = new PolyhedralSurface();

                    // Perform polyhedralization.
                    switch (geomType)
                    {
                        case InputGeometryType.PolyhedralSurfaceZ:
                            polyhedral.Dimension = Dimension.Xyz;
                            polyhedral = (PolyhedralSurface)building.Geometry;
                            break;

                        case InputGeometryType.MultiPolygonZ:
                            polyhedral = TesselateBuilding.ToPolyhedral((MultiPolygon)building.Geometry);
                            break;

                        case InputGeometryType.Polygon:
                            var polygon = (Polygon)building.Geometry;
                            var wktFootprint = polygon.SerializeString<WktSerializer>();
                            var height = building.Height;
                            var points = polygon.ExteriorRing.Points;

                            var buildingZ = 0; //put everything on the ground
                            polyhedral = TesselateBuilding.MakeBuilding(polygon, buildingZ, height);
                            break;
                    }

                    // Geometry to wkt format
                    var wkt = polyhedral.SerializeString<WktSerializer>();

                    // Update table row
                    var updateSql = $@"update {o.Table} set {outputGeometryColumn} = 
                                        ST_Transform(ST_Force3D(St_SetSrid(ST_GeomFromText('{wkt}'), {inputGeometry.Srid})), {outputProjection}) 
                                        where {o.IdColumn}={building.Id};";
                    conn.Execute(updateSql);

                    // Progress bar logic
                    var perc = Math.Round((double)i / buildings.AsList().Count * 100, 2);
                    Console.Write($"\rProgress: {perc.ToString("F")}%");
                    i++;
                }

                // Add shaders
                Console.WriteLine("");
                Console.WriteLine("Adding shaders...");
                conn.Execute($"alter table {o.Table} drop column if exists {outputGeometryColumn}_shader cascade;");
                conn.Execute($"alter table {o.Table} add {outputGeometryColumn}_shader jsonb;");

                var styleSql = @$"    
                    update {o.Table} set {outputGeometryColumn}_shader = jsonb_build_object(	
                        'PbrMetallicRoughness', jsonb_build_object(
                            'BaseColors'::text, array_fill('{o.Color}'::text, ARRAY[ST_NumGeometries({outputGeometryColumn})]), 
                        'MetallicRoughness'::text, array_fill('#008000'::text, ARRAY[ST_NumGeometries({outputGeometryColumn})])));
                ";
                conn.Execute(styleSql);

                // Filter out wrongly tesselated buildings.
                var deleteSql = @$"
                    delete from {o.Table} 
                    where ST_NumGeometries({outputGeometryColumn}) is null 
                    and ST_NumGeometries({outputGeometryColumn}) = 0 
                    and ST_Npoints({outputGeometryColumn})::numeric/st_numgeometries({outputGeometryColumn})::numeric != 4; 
                ";
                conn.Execute(deleteSql);

                // Create index on triangulated geometry column
                conn.Execute($"create index on {o.Table} using gist(st_centroid(st_envelope({outputGeometryColumn})));");

                // Close connection
                conn.Close();

                stopWatch.Stop();
                Console.WriteLine();
                Console.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds / 1000} seconds");
                Console.WriteLine("Program finished.");
            });
        }
    }
}
