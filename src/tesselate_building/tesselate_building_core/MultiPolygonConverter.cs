using System;
using tesselate_building_core;
using Wkx;

public class MultiPolygonConverter : Converter
    {
        public override PgGeometry Convert(PgGeometry geometry, Building building)
        {
            MultiPolygon multiPolygon = (MultiPolygon)geometry.Geometry;
            var polyhedral = TesselateBuilding.ToPolyhedral(multiPolygon);
            
            return new PgGeometry(polyhedral);
        }
    }