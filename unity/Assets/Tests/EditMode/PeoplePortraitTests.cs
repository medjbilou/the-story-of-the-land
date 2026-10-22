using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game.UI;
using UnityEngine;

/// <summary>Owner, 2026-10-21: every figure on the People screen has its own picture (Resources/People/&lt;id&gt;), small
/// enough for the web build.</summary>
public class PeoplePortraitTests
{
    [Test]
    public void EveryFigureHasItsPicture()
    {
        var figures = Figures.Parse(FamilyKit.Inputs.Read(Figures.FileName)).All;
        Assert.Greater(figures.Count, 0);
        var missing = figures.Where(f => Resources.Load<Texture2D>(PeoplePresenter.PortraitFolder + f.Id) == null).Select(f => f.Id).ToList();
        CollectionAssert.IsEmpty(missing, "figures without a picture");
        foreach (var f in figures)
        {
            var t = Resources.Load<Texture2D>(PeoplePresenter.PortraitFolder + f.Id);
            Assert.LessOrEqual(Mathf.Max(t.width, t.height), 512, f.Id + ": at most 512 px");
        }
    }
}
