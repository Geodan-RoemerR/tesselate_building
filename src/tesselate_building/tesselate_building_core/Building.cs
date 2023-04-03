using System.Text.Json;
using Wkx;

namespace tesselate_building_core
{
    public class Building
    {
        public Wkx.Geometry Geometry { get; set; }
        public double Height { get; set; }

        public int Id { get; set; }

        public string Style { get; set; } 

    }
}
