using NUnit.Framework;
using System.Collections.Generic;
using tesselate_building_core;
using Wkx;

namespace NUnitTestProject1
{
    public class Tests
    {
        private string wktFootprint;
        private double height;
        private Polygon footprint;

        private string wktMultiPolygon;
        private MultiPolygon multiPolygon;

        [SetUp]
        public void Setup()
        {
            wktFootprint = "POLYGON((-75.55478134 39.1632752950001,-75.55477116 39.163235817,-75.554760981 39.1631963390001,-75.554818218 39.163187394,-75.5548754549999 39.16317845,-75.5548856349999 39.1632179280001,-75.554896589 39.1632604100001,-75.554724403 39.163285407,-75.554724102 39.1632842400001,-75.55478134 39.1632752950001))";
            footprint = (Polygon)Wkx.Geometry.Deserialize<WktSerializer>(wktFootprint);
            height = 11.55;

            wktMultiPolygon = "MULTIPOLYGON Z (((115500.42958007813 494951.8741015625 3.304316759109497,115500.42958007813 494951.8741015625 -0.123000003397465,115500.67091308594 494951.7744921875 -0.123000003397465,115500.67091308594 494951.7744921875 3.304316759109497,115500.42958007813 494951.8741015625 3.304316759109497)),((115511.40998779297 494945.7689990234 3.304316759109497,115511.40998779297 494945.7689990234 -0.123000003397465,115510.86201416016 494944.3640002441 -0.123000003397465,115510.86201416016 494944.3640002441 3.304316759109497,115511.40998779297 494945.7689990234 3.304316759109497)))";
            multiPolygon = (MultiPolygon)Wkx.Geometry.Deserialize<WktSerializer>(wktMultiPolygon);
        }

        [Test]
        public void MakeBuildingTest()
        {
            var polyhedral = TesselateBuilding.MakeBuilding(footprint, 0, height);
            var wkt = polyhedral.SerializeString<WktSerializer>();
            Assert.IsTrue(wkt != null);

        }

        [Test]
        public void TestId12()
        {
            var wktFootprint = "POLYGON((-75.554412769 39.1634003080001, -75.554480102 39.163362636, -75.554508552 39.1633934610001, -75.554552455 39.163368898, -75.554609356 39.1634305470001, -75.554505101 39.163488876, -75.554412769 39.1634003080001))";
            footprint = (Polygon)Wkx.Geometry.Deserialize<WktSerializer>(wktFootprint);
            var height = 9.92000000000;

            var polyhedral = TesselateBuilding.MakeBuilding(footprint, 0, height);
            Assert.IsTrue(polyhedral.Geometries.Count == 20);
        }

        [Test]
        public void MakePolyhedralTest()
        {
            var polyhedral = TesselateBuilding.ToPolyhedral(multiPolygon);
            var wkt = polyhedral.SerializeString<WktSerializer>();
            Assert.IsTrue(polyhedral.Geometries.Count == 2);
            Assert.IsTrue(polyhedral.Geometries[0].ExteriorRing.Points.Count == 2);
            Assert.IsTrue(wkt != null);
        }

        [Test]
        public void TriangulateBuildingTest()
        {
            var polyhedral = TesselateBuilding.MakeBuilding(footprint, 0, height);
            Assert.IsTrue(polyhedral.Geometries.Count == 30);
        }


        [Test]
        public void MakeWallsTest()
        {
            var walls = TesselateBuilding.MakeWalls(footprint, 0, height);
            Assert.IsTrue(walls.Count == (footprint.ExteriorRing.Points.Count - 1));
        }
    }
}