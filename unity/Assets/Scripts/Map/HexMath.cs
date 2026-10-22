using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>Pointy-top hex geometry (Red Blob Games): corner i at 60*i - 30 degrees.</summary>
    public static class HexMath
    {
        public static Vector2 Corner(Vector2 center, float sizeM, int i)
        {
            float a = Mathf.Deg2Rad * (60f * i - 30f);
            return new Vector2(center.x + sizeM * Mathf.Cos(a), center.y + sizeM * Mathf.Sin(a));
        }
    }
}
