using System;
using tesselate_building_core;
using Wkx;

public class PolyhedralConverter : Converter
{
    public override PolyhedralSurface Convert(Geometry geometry, Building building)
    {
        var polyhedral = new PolyhedralSurface();
        polyhedral.Dimension = Dimension.Xyz;
        polyhedral = TesselateBuilding.ToPolyhedral(geometry.Geom);
        return polyhedral;
    }
}