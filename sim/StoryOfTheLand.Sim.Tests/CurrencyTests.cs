using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using StoryOfTheLand.Sim;

/// <summary>The family's money by date and village region (content/currency.tsv -> palestine/currency.yaml, docs/palestine-model.md).</summary>
public class CurrencyTests
{
    static readonly string Root = FamilyTests.Root;
    static readonly string[] ChapterIds = { "1880_1946", "1947_1949", "1950_1966", "1967_1987", "1987_2000", "2000_2023", "2023_2026" };
    static readonly Dictionary<string, Content> Chapters = ChapterIds.ToDictionary(id => id, id => ContentLoader.LoadChapter(Root, id));
    static readonly Content Any = Chapters["1967_1987"];   // the currency data is the same in every chapter

    static Currency At(string village, int year, int month) =>
        Family.CurrencyAt(Any, village, new DateTime(year, month, 1)) ?? throw new Xunit.Sdk.XunitException($"no currency for {village} {year}-{month}");

    [Theory]
    [InlineData("tantura", 1948, 5, "palestine_pound_1948", "Palestine pound", "£P", "ج.ف")]
    [InlineData("tantura", 1947, 11, "palestine_pound", "Palestine pound", "£P", "ج.ف")]
    [InlineData("jenin", 1967, 6, "jordanian_dinar_wb", "Jordanian dinar", "JD", "د.أ")]
    [InlineData("gaza", 1956, 6, "egyptian_pound_gz", "Egyptian pound", "£E", "ج.م")]
    [InlineData("khan_yunis", 2023, 10, "israeli_new_shekel_gz_1994", "Israeli new shekel (under occupation)", "₪", "ش.ج")]
    [InlineData("khan_yunis", 1990, 1, "israeli_new_shekel_gz", "Israeli new shekel (imposed by the occupation)", "₪", "ش.ج")]
    public void TheFamilysMoneyFollowsTheDateAndTheVillagesRegion(string village, int year, int month, string id, string name, string symbol, string symbolAr)
    {
        var cur = At(village, year, month);
        Assert.Equal(id, cur.Id);
        Assert.Equal(name, cur.Name);
        Assert.Equal(symbol, cur.Symbol);
        Assert.Equal(symbolAr, cur.SymbolAr);
        Assert.NotEmpty(cur.Sources);
    }

    [Fact]
    public void TheRegionsSplitAfter1948AndTheOccupationIsNamed()
    {
        Assert.Equal("inside_1948", Family.Region(Any, "tantura"));
        Assert.Equal("west_bank", Family.Region(Any, "jenin"));
        Assert.Equal("west_bank", Family.Region(Any, "imwas"));
        Assert.Equal("gaza", Family.Region(Any, "khan_yunis"));
        Assert.Equal("israeli_pound_wb", At("jenin", 1967, 7).Id);            // the occupation of 5-7 June shows from July
        Assert.Equal("israeli_pound_gz_1956", At("gaza", 1957, 1).Id);        // the decree of 3 December 1956
        Assert.Equal("egyptian_pound_gz_1957", At("gaza", 1957, 4).Id);
        Assert.Equal("ottoman_lira", At("jenin", 1900, 1).Id);
        Assert.Equal("egyptian_pound_1918", At("gaza", 1920, 1).Id);
    }

    /// <summary>Inside the 1949 lines the state's first notes (Anglo-Palestine Bank, 18 August 1948) were named "Palestine Pound";
    /// the prutah replaced the mil in 1949 (first prutah coin 11 May), the Israeli pound came on 9 June 1952, and the agora
    /// replaced the prutah on 1 January 1960 (Bank of Israel; audits/arabic_currency.md, Resolved).</summary>
    [Theory]
    [InlineData("tantura", 1948, 9, "palestine_pound_apb_1948", "المِل، والجنيه ألف مِل")]
    [InlineData("tantura", 1949, 5, "palestine_pound_apb_1948", "المِل، والجنيه ألف مِل")]
    [InlineData("tantura", 1949, 6, "palestine_pound_prutah_1948", "البروطة، وفي الجنيه ألف منها")]
    [InlineData("tantura", 1952, 6, "palestine_pound_prutah_1948", "البروطة، وفي الجنيه ألف منها")]
    [InlineData("tantura", 1952, 7, "israeli_pound_1948", "البروطة، وفي الليرة ألف منها")]
    [InlineData("tantura", 1959, 12, "israeli_pound_1948", "البروطة، وفي الليرة ألف منها")]
    [InlineData("tantura", 1960, 1, "israeli_pound_agora_1948", "الأغورة، وفي الليرة مئة منها")]
    [InlineData("jenin", 1967, 7, "israeli_pound_wb", "الأغورة، وفي الليرة مئة منها")]
    [InlineData("gaza", 1957, 1, "israeli_pound_gz_1956", "البروطة، وفي الليرة ألف منها")]
    [InlineData("gaza", 1967, 7, "israeli_pound_gz", "الأغورة، وفي الليرة مئة منها")]
    public void TheNameAndSubunitFollowTheDate(string village, int year, int month, string id, string subunitAr)
    {
        var cur = At(village, year, month);
        Assert.Equal(id, cur.Id);
        Assert.Equal(subunitAr, cur.SubunitAr);
        Assert.DoesNotContain(year >= 1960 ? "prutah" : "agora", cur.Subunit);
    }

    /// <summary>One wording a tier, in both languages: inside the 1949 lines "imposed by the occupying state"; the West Bank
    /// and Gaza from 1967 to the Paris Protocol "imposed by the occupation"; from May 1994 "under occupation".</summary>
    [Theory]
    [InlineData("tantura", 1948, 9, "Palestine pound (imposed by the occupying state)", "الجنيه الفلسطيني (فرضته دولة الاحتلال)")]
    [InlineData("tantura", 1960, 1, "Israeli pound (imposed by the occupying state)", "الليرة الإسرائيلية (فرضتها دولة الاحتلال)")]
    [InlineData("tantura", 1985, 1, "Israeli shekel (imposed by the occupying state)", "الشيكل الإسرائيلي (فرضته دولة الاحتلال)")]
    [InlineData("tantura", 2023, 10, "Israeli new shekel (imposed by the occupying state)", "الشيكل الإسرائيلي الجديد (فرضته دولة الاحتلال)")]
    [InlineData("jenin", 1970, 1, "Israeli pound (imposed by the occupation)", "الليرة الإسرائيلية (فرضها الاحتلال)")]
    [InlineData("gaza", 1957, 1, "Israeli pound (imposed by the occupation)", "الليرة الإسرائيلية (فرضها الاحتلال)")]
    [InlineData("jenin", 1985, 1, "Israeli shekel (imposed by the occupation)", "الشيكل الإسرائيلي (فرضه الاحتلال)")]
    [InlineData("jenin", 1994, 4, "Israeli new shekel (imposed by the occupation)", "الشيكل الإسرائيلي الجديد (فرضه الاحتلال)")]
    [InlineData("khan_yunis", 1994, 4, "Israeli new shekel (imposed by the occupation)", "الشيكل الإسرائيلي الجديد (فرضه الاحتلال)")]
    [InlineData("jenin", 1994, 5, "Israeli new shekel (under occupation)", "الشيكل الإسرائيلي الجديد (تحت الاحتلال)")]
    [InlineData("khan_yunis", 2023, 10, "Israeli new shekel (under occupation)", "الشيكل الإسرائيلي الجديد (تحت الاحتلال)")]
    public void TheOccupationIsNamedTheSameWayInEachTier(string village, int year, int month, string name, string nameAr)
    {
        var cur = At(village, year, month);
        Assert.Equal(name, cur.Name);
        Assert.Equal(nameAr, cur.NameAr);
        if (village != "tantura") Assert.Equal(cur.From >= new DateTime(1994, 5, 1), cur.Note.Contains("Paris Protocol"));
    }

    [Fact]
    public void EveryIsraeliCurrencyNamesTheOccupation()
    {
        foreach (var x in Any.Currencies.Where(x => x.Id.StartsWith("israeli_", StringComparison.Ordinal) || x.Region == "inside_1948" && x.From >= new DateTime(1948, 9, 1)))
            Assert.Matches(@"\((imposed by the occupying state|imposed by the occupation|under occupation)\)$", x.Name);
    }

    [Fact]
    public void TheLivelihoodLineShowsTheCurrencyOfTheTurn()
    {
        var g = GameSession.New(Chapters["2023_2026"], 1, "khan_yunis");
        Assert.Equal(new DateTime(2023, 10, 1), g.State.Date);
        Assert.Equal("Livelihood 50% · Israeli new shekel (under occupation), ₪", PlayerText.Livelihood(g.Content, g.State));
        Assert.Equal("الشيكل الإسرائيلي الجديد (تحت الاحتلال)، ش.ج", PlayerText.Currency(g.Content, g.State, arabic: true));
        Assert.Equal("الرزق 50% · الشيكل الإسرائيلي الجديد (تحت الاحتلال)، ش.ج", PlayerText.Livelihood(g.Content, g.State, arabic: true));
        var t = GameSession.New(Chapters["1947_1949"], 1, "tantura");
        Assert.Equal("Palestine pound, £P", PlayerText.Currency(t.Content, t.State));
        Assert.Equal("الرزق 50% · الجنيه الفلسطيني، ج.ف", PlayerText.Livelihood(t.Content, t.State, arabic: true));
    }

    /// <summary>The sign is left out where it only repeats a word of the name: the HUD never says «الليرة العثمانية، ليرة».</summary>
    [Fact]
    public void TheHudDoesNotRepeatTheNameAsItsSign()
    {
        var g = GameSession.New(Chapters["1880_1946"], 1, "jenin");
        Assert.Equal("ottoman_lira", Family.CurrencyAt(g.Content, g.State)!.Id);
        Assert.Equal("ليرة", Family.CurrencyAt(g.Content, g.State)!.SymbolAr);
        Assert.Equal("الليرة العثمانية", PlayerText.Currency(g.Content, g.State, arabic: true));
        Assert.Equal("Ottoman lira, LT", PlayerText.Currency(g.Content, g.State));
        Assert.Equal("الرزق 50% · الليرة العثمانية", PlayerText.Livelihood(g.Content, g.State, arabic: true));
    }

    static readonly Regex Il = new Regex(@"\bIL\b|Foreign currency");

    [Fact]
    public void NoFamilyTextShowsTheOldMoney()
    {
        foreach (var c in Chapters.Values)
        {
            Assert.Throws<InvalidOperationException>(() => PlayerText.Change(c, "fx_reserves", 1));
            foreach (var village in new[] { "tantura", "jenin", "khan_yunis" })
            {
                var g = GameSession.New(c, 1, village);
                var texts = new List<string>();
                while (g.State.GameOver == null)
                {
                    texts.Add(PlayerText.Livelihood(c, g.State));
                    texts.Add(PlayerText.Currency(c, g.State, arabic: true));
                    texts.Add(PlayerText.Livelihood(c, g.State, arabic: true));
                    texts.AddRange(g.Step(Array.Empty<Command>()).Why.Select(w => w.Message));
                }
                Assert.DoesNotContain(texts, t => Il.IsMatch(t));
            }
        }
        Assert.DoesNotContain(Any.Currencies, x => Il.IsMatch(x.Name + " " + x.Symbol + " " + x.Note));
    }

    // The converter's round trip, from the sim's side: currency.yaml as the sim reads it gives back the TSV rows exactly.
    static List<string[]> Tsv(string name) =>
        File.ReadAllLines(Path.Combine(Root, "..", "..", "content", name)).Where(l => l.Length > 0 && !l.StartsWith("#")).Select(l => l.Split('\t')).ToList();

    static string N(int? v) => v?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";

    [Fact]
    public void CurrencyYamlRoundTripsToTheTsv()
    {
        // The audited TSV lives beside the repository (../content); a clone of the game alone checks the YAML elsewhere.
        if (!File.Exists(Path.Combine(Root, "..", "..", "content", "currency.tsv"))) return;
        var rows = Tsv("currency.tsv");
        Assert.Equal(rows.Count, Any.Currencies.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            var x = Any.Currencies[i];
            Assert.Equal(rows[i], new[] { x.Id, N(x.FromYear), N(x.FromMonth), N(x.ToYear), N(x.ToMonth), x.Region, x.NameAr, x.Name,
                x.SymbolAr, x.Symbol, x.SubunitAr, x.Subunit, x.Note, string.Join(" ", x.Sources) });
        }
        var regions = Tsv("place_regions.tsv");
        Assert.Equal(regions, Any.PlaceRegions.Select(r => new[] { r.Place, r.Region, r.Note, string.Join(" ", r.Sources) }).ToList());
    }

    const string Model = "indicators:\n  - { id: land, base: 0.5, name: Land, name_ar: \"الأرض\" }\n";
    const string Chapter = "events:\n  - id: e\n    title: T\n    text: X\n    scope: national\n    from: 1948-01-01\n    to: 1948-03-01\n"
        + "    options:\n      - { id: a, text: A }\n    sources:\n      - { url: \"https://example.org\", checked: 2026-10-19 }\n";
    const string Places = "places:\n  - { id: tantura, name: Tantura, name_ar: \"الطنطورة\", lon: 34.9177, lat: 32.6094, sources: [\"https://example.org\"] }\n";

    static Content Load(string currency) =>
        ContentLoader.LoadChapter(rel => rel == "model.yaml" ? Model : rel == "events/test.yaml" ? Chapter : rel == "places.yaml" ? Places
            : rel == "fates.yaml" ? "fates: []\n" : rel == "currency.yaml" ? currency : null, "test");

    const string Pound = "currencies:\n  - { id: p, from_year: 1927, from_month: 11, to_year: 1948, to_month: 3, region: national, name: P, name_ar: \"ج\", symbol: P, symbol_ar: \"ج\", sources: [\"https://example.org\"] }\nregions: []\n";

    [Theory]
    [InlineData("to_month: 3", "to_month: 2", "0 currencies for inside_1948 in 1948-02")]
    [InlineData("regions: []", "regions:\n  - { place: haifa, region: gaza, sources: [\"https://example.org\"] }", "unknown place 'haifa'")]
    [InlineData("region: national", "region: galilee", "region")]
    [InlineData("name: P,", "name: \"\",", "name")]
    public void TheLoaderRefusesAGapOrABadRow(string from, string to, string message)
    {
        Assert.Single(Load(Pound).Currencies);
        var ex = Assert.Throws<ContentException>(() => Load(Pound.Replace(from, to)));
        Assert.Contains(message, ex.Message);
    }
}
