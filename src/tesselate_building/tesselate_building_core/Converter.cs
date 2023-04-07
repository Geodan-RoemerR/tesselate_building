using tesselate_building_core;
using Wkx;

public abstract class Converter
{
    public abstract PolyhedralSurface Convert(Geometry geometry, Building building);
}