using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryOfTheLand.Sim
{
    /// <summary>The release gate for sensitive content (spec 5.5; M3 final review I4). An event with a sensitive tag
    /// (holocaust, terror, massacre, war-casualties, displacement, sensitive) needs the owner's sensitivity review, recorded by moving its YAML
    /// <c>status</c> to <c>sensitivity-reviewed</c>, before any build is shared. Build -shareable (Web or Mac) refuses to build while
    /// <see cref="SensitiveUnreviewed"/> is not empty for any chapter. Setting that status is an OWNER step (docs/RELEASING.md).</summary>
    public static class ReleaseGate
    {
        public const string ReviewedStatus = "sensitivity-reviewed";

        /// <summary>The sensitive events whose status is not yet sensitivity-reviewed, in id order.</summary>
        public static IReadOnlyList<string> SensitiveUnreviewed(Content c) =>
            c.Events.Where(e => e.Tags.Any(EventRules.SensitiveTags.Contains) && e.Status != ReviewedStatus)
                .Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }
}
