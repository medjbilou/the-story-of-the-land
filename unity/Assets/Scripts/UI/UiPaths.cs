#nullable enable
namespace StoryOfTheLand.Game.UI
{
    /// <summary>Project paths of the UI assets, shared by the editor scripts (Assets/Editor) and the edit-mode tests,
    /// which cannot reference the editor assembly.</summary>
    public static class UiPaths
    {
        public const string Folder = "Assets/UI";
        public const string Panel = Folder + "/StoryOfTheLandPanel.asset";
        public const string Theme = Folder + "/StoryOfTheLandTheme.tss";
        public const string GameUxml = Folder + "/Game.uxml";
        public const string Strings = Folder + "/strings.en.yaml";
        /// <summary>The Arabic strings, loaded when Arabic is asked for (UiLanguage; #193).</summary>
        public const string StringsArabic = Folder + "/strings.ar.yaml";
        public const string FontRegular = Folder + "/Fonts/EBGaramond-Regular.ttf";
        public const string FontSemiBold = Folder + "/Fonts/EBGaramond-SemiBold.ttf";
        /// <summary>The Arabic face (EB Garamond has no Arabic; #193): Noto Naskh Arabic, OFL.</summary>
        public const string FontArabic = Folder + "/Fonts/NotoNaskhArabic/NotoNaskhArabic.ttf";
        /// <summary>The Arabic credits' faces: Amiri Regular and Bold, OFL (StoryOfTheLand.uss, .lang-ar .credits-*).</summary>
        public const string FontCredits = Folder + "/Fonts/Amiri/Amiri-Regular.ttf";
        public const string FontCreditsBold = Folder + "/Fonts/Amiri/Amiri-Bold.ttf";
    }
}
