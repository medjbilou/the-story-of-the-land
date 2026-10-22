#nullable enable
using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The hex grid's distance field, the C# twin of ScHexEdgeDistance (StoryOfTheLandMap.hlsl), on tools/hexgrid.py's
    /// layout: pointy-top axial hexes of circumradius size; (q, r) = (0, 0) is centred on the map's north-west corner
    /// (lon0, lat1); x grows east and y south, in metres.</summary>
    public static class HexGrid
    {
        const float Sqrt3 = 1.7320508f, HalfSqrt3 = 0.8660254f;

        public static Vector2 Centre(int q, int r, float size) => new Vector2(size * (Sqrt3 * q + HalfSqrt3 * r), size * 1.5f * r);

        /// <summary>The centre of the hex containing p (cube rounding, Red Blob Games).</summary>
        public static Vector2 NearestCentre(Vector2 p, float size)
        {
            float qf = (Sqrt3 / 3f * p.x - p.y / 3f) / size, rf = 2f / 3f * p.y / size, sf = -qf - rf;
            float q = Mathf.Round(qf), r = Mathf.Round(rf), s = Mathf.Round(sf);
            float dq = Mathf.Abs(q - qf), dr = Mathf.Abs(r - rf), ds = Mathf.Abs(s - sf);
            if (dq > dr && dq > ds) q = -r - s; else if (dr > ds) r = -q - s;
            return Centre((int)q, (int)r, size);
        }

        /// <summary>The grid point (x east, y south of the north-west corner) of a world point on a map whose terrain origin
        /// (south-west corner) is <paramref name="origin"/>.</summary>
        public static Vector2 FromWorld(Vector3 world, Vector3 origin, float lengthM) => new Vector2(world.x - origin.x, lengthM - (world.z - origin.z));

        /// <summary>The inverse of FromWorld, at the origin's height.</summary>
        public static Vector3 ToWorld(Vector2 p, Vector3 origin, float lengthM) => new Vector3(origin.x + p.x, origin.y, origin.z + lengthM - p.y);

        /// <summary>Distance (m) from p to the nearest edge of the hex containing it: the inradius at a centre, 0 on an edge.</summary>
        public static float EdgeDistance(Vector2 p, float size)
        {
            var v = p - NearestCentre(p, size);
            float m = Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(0.5f * v.x + HalfSqrt3 * v.y), Mathf.Abs(0.5f * v.x - HalfSqrt3 * v.y)));
            return HalfSqrt3 * size - m;
        }
    }
}
