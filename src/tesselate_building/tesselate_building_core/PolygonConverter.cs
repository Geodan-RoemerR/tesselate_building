using System;
using tesselate_building_core;
using Wkx;

public class PolygonConverter : Converter
    {


        public override PgGeometry Convert(PgGeometry geometry, Building building)
        {
            var polygon = (Polygon)geometry.Geometry;
            var wktFootprint = polygon.SerializeString<WktSerializer>();
            var height = building.Height;
            var points = polygon.ExteriorRing.Points;

            var buildingZ = 0; //put everything on the ground
            var polyhedral = TesselateBuilding.MakeBuilding(polygon, buildingZ, height);
            return new PgGeometry(polyhedral);
        }
    }