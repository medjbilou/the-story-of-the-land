#nullable enable
using System;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>lon/lat -> axial hex, the same projection and rounding as tools/hexgrid.py (lonlat_to_km + point_to_hex):
    /// x km east of lon0, y km south of lat1, pointy-top hexes, cube rounding. Used for map clicks and the minimap; the sim
    /// never needs it (settlement hexes come precomputed from tools/settlement_hexes.py).</summary>
    public static class HexPicker
    {
        public const double KmPerDegLat = 110.57, KmPerDegLonEq = 111.32;   // tools/geo.py

        public static double KmPerDegLon(double lat0, double lat1) => KmPerDegLonEq * Math.Cos((lat0 + lat1) / 2 * (Math.PI / 180));   // x * (pi/180) is how Python's math.radians computes it, bit for bit

        public static (int q, int r) FromLonLat(double lon, double lat, HexBounds b, double hexSizeKm)
        {
            var x = (lon - b.lon0) * KmPerDegLon(b.lat0, b.lat1);
            var y = (b.lat1 - lat) * KmPerDegLat;
            var qf = (Math.Sqrt(3) / 3 * x - y / 3) / hexSizeKm;
            var rf = (2.0 / 3 * y) / hexSizeKm;
            var sf = -qf - rf;
            double q = Math.Round(qf, MidpointRounding.ToEven), r = Math.Round(rf, MidpointRounding.ToEven), s = Math.Round(sf, MidpointRounding.ToEven);
            double dq = Math.Abs(q - qf), dr = Math.Abs(r - rf), ds = Math.Abs(s - sf);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            return ((int)q, (int)r);
        }
    }
}
