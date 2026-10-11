// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.CustomFps.Beatmaps;
using osu.Game.Rulesets.CustomFps.Configuration;
using osu.Game.Rulesets.CustomFps.UI;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osuTK.Graphics;

namespace osu.Game.Rulesets.CustomFps
{
    public partial class CustomFpsRuleset : Ruleset
    {
        public override string Description => "Custom frame limiter";

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod> mods = null) =>
            new DrawableCustomFpsRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) =>
            new CustomFpsBeatmapConverter(beatmap, this);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) =>
            new CustomFpsDifficultyCalculator(RulesetInfo, beatmap);

        public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();

        public override IRulesetConfigManager CreateConfig(SettingsStore settings) =>
            new CustomFpsRulesetConfigManager(settings, RulesetInfo);

        public override RulesetSettingsSubsection CreateSettings() =>
            new CustomFpsSettingsSubsection(this);

        public override string ShortName => "cfps";

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => Array.Empty<KeyBinding>();

        /// <summary>
        /// An infinity glyph encircled by a deliberately broken ring: frame limits, unshackled.
        /// </summary>
        public override Drawable CreateIcon() => new UnshackledIcon();

        private partial class UnshackledIcon : CompositeDrawable
        {
            public UnshackledIcon()
            {
                AutoSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Position = new osuTK.Vector2(48),
                        Origin = Anchor.Centre,
                        Icon = FontAwesome.Solid.Infinity,
                        Size = new osuTK.Vector2(32),
                    },
                    createBrokenRing(),
                };
            }

            /// <summary>
            /// Samples a circle leaving two gaps at the top and bottom, so the ring reads as "broken open" rather
            /// than a closed border around the infinity.
            /// </summary>
            private static Path createBrokenRing()
            {
                const float radius = 44;
                const int samples = 96;

                var vertices = new List<osuTK.Vector2>();

                for (int i = 0; i <= samples; i++)
                {
                    float t = MathF.PI / 2 + MathF.PI * 0.14f + (MathF.PI * 2 - MathF.PI * 0.28f) * i / samples;
                    vertices.Add(new osuTK.Vector2(48 + MathF.Cos(t) * radius, 48 + MathF.Sin(t) * radius));
                }

                return new Path
                {
                    PathRadius = 3,
                    Colour = new Color4(1f, 0.4f, 0.67f, 1f),
                    Vertices = vertices,
                };
            }
        }

        // Leave this line intact. It will bake the correct version into the ruleset on each build/release.
        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
    }
}