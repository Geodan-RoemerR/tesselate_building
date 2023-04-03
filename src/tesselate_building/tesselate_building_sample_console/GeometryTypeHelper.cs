using Dapper;
using System.Data;
using Wkx;

namespace tesselate_building_sample_console
{
    public class GeometryTypeHandler : SqlMapper.TypeHandler<Wkx.Geometry>
    {
        public override Wkx.Geometry Parse(object value)
        {

            var stream = (byte[])value;
            var g = Wkx.Geometry.Deserialize<WkbSerializer>(stream);
            return g;
        }

        public override void SetValue(IDbDataParameter parameter, Wkx.Geometry value)
        {
            var g = value.SerializeByteArray<WkbSerializer>();
            parameter.Value = g;
        }
    }
}
