namespace StoryOfTheLand.Game.Map.Units
{
    /// <summary>Where the painted-miniature material lives (the trains and road vehicles use it): in Resources, so players load it without Shader.Find (M0 correction 1) and without a new
    /// serialized field on MapScene. Made by the editor's UnitMaterialAssets.Create.</summary>
    public static class UnitMaterials
    {
        public const string Folder = "Assets/Resources/Units";
        /// <summary>Matte URP Lit; each user sets its palette texture on a runtime copy.</summary>
        public const string MiniatureResource = "Units/UnitMiniature";
    }
}
