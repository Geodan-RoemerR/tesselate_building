using CommandLine;

namespace tesselate_building_sample_console
{
    public class Options
    {

        [Option('U', "username", Required = false, HelpText = "Database user")]
        public string User { get; set; }

        [Option('h', "host", Required = false, Default = "localhost", HelpText = "Database host")]
        public string Host { get; set; }

        [Option('d', "dbname", Required = false, HelpText = "Database name")]
        public string Database { get; set; }

        [Option('p', "port", Required = false, Default = "5432", HelpText = "Database port")]
        public string Port { get; set; }

        [Option('t', "table", Required = true, HelpText = "Database table, include database schema if needed")]
        public string Table { get; set; }

        [Option('i', "inputgeometrycolumn", Required = false, Default = "geom", HelpText = "Input geometry column")]
        public string InputGeometryColumn { get; set; }

        [Option("heightcolumn", Required = false, Default = "height", HelpText = "height column")]
        public string HeightColumn { get; set; }

        [Option("idcolumn", Required = false, Default = "id", HelpText = "Id column")]
        public string IdColumn { get; set; }

        [Option("color", Required = false, Default = "#FFFFFF", HelpText = "Color in hex")]
        public string Color { get; set; }

    }
}
