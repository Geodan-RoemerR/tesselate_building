using tesselate_building_core;
using Wkx;

public class Geometry : Element
{
    public Geometry(Wkx.Geometry geometry)
    {
        this.Geom = geometry;
    }

    public Wkx.Geometry Geom { get; set; }

    public override void Convert(Converter converter)
    {
        converter.Convert(this, new Building());
    }
}
