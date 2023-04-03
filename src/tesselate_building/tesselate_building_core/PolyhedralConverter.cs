using System;
using tesselate_building_core;
using Wkx;

public class PolyhedralConverter : Converter
    {
        public override PgGeometry Convert(PgGeometry geometry, Building building)
        {
            var polyhedral = new PolyhedralSurface();
            polyhedral.Dimension = Dimension.Xyz;
            polyhedral = (PolyhedralSurface)building.Geometry;
            return geometry;
        }
    }