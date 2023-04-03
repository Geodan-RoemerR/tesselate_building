using tesselate_building_core;
using Wkx;

public class PgGeometry : Element 
{    
    public PgGeometry(Geometry geometry) {
        this.Geometry = geometry;
    }

    public Geometry Geometry { get; set; }

    public override void Convert(Converter converter) {
        converter.Convert(this, new Building());
    }
}
