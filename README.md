# tesselate_building

Console tool for creating triangulated polyhedralsurface from MultiPolygonZ, PolyhedralSurfaceZ and (building) footprint through height value. Shaders per triangle are written to the '{inputgeometrycolumn}_3d_triangle shaders' column.

This tool is designed to create the correct input information for creating 3D tiles with pg2b3dm (https://github.com/Geodan/pg2b3dm). This tool is used in the pg2b3dm 'getting started' sample see https://github.com/Geodan/pg2b3dm/blob/master/getting_started.md


## Running

```
$ tesselate_building -U postgres -h localhost -d research -t bro.geotop3d
```

## command line options

All parameters are optional, except the -t --table option.

If --username and/or --dbname are not specified the current username is used as default.

```
 -U, --username                Database user

  -h, --host                    (Default: localhost) Database host

  -d, --dbname                  Database name

  -p, --port                    (Default: 5432) Database port

  -t, --table                   Required. Database table, include database schema if needed

  -i, --inputgeometrycolumn     (Default: geom) Input geometry column

  --heightcolumn                (Default: height) height column

  --idcolumn                    (Default: id) Id column
  
  --color                       (Default: #FFFFFF) Batched model color in hex

  --help                        Display this help screen.

  --version                     Display version information.
  ```

## Dependencies

- CommandLineParser https://github.com/commandlineparser/commandline
- Dapper https://github.com/StackExchange/Dapper
- Npgsql https://github.com/npgsql/npgsql
- Wkx https://github.com/cschwarz/wkx-sharp
- Triangulator https://github.com/bertt/triangulator

## History

2023-03-30: forked and added MultiPolygonZ and PolyhedralSurfaceZ support, as well as automatically creating shader and output column.
2022-01-24: release 0.2 to from .NET 5 to .NET 6
