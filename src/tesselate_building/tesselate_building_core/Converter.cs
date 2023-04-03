using tesselate_building_core;

public abstract class Converter
{
    public abstract Geometry Convert(Geometry geometry, Building building);
}