using tesselate_building_core;

public abstract class Converter
{
    public abstract PgGeometry Convert(PgGeometry geometry, Building building);
}