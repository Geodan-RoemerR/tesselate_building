using System;
using tesselate_building_core;
using Wkx;

public class MultiPolygonConverter : Converter
{
    public override Geometry Convert(Geometry geometry, Building building)
    {
        MultiPolygon multiPolygon = (MultiPolygon)geometry.Geom;
        var polyhedral = TesselateBuilding.ToPolyhedral(multiPolygon);

        return new Geometry(polyhedral);
    }
}