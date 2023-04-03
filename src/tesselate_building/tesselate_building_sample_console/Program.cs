using CommandLine;

namespace tesselate_building_sample_console
{
    class Program
    {
        static void Main(string[] args)
        {
            // Run application
            Application.run(Parser.Default.ParseArguments<Options>(args));
        }
    }
}
