// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.CustomFps.Objects;
using osu.Game.Rulesets.CustomFps.Objects.Drawables;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.CustomFps.UI
{
    [Cached]
    public partial class DrawableCustomFpsRuleset : DrawableRuleset<CustomFpsHitObject>
    {
        public DrawableCustomFpsRuleset(CustomFpsRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override Playfield CreatePlayfield() => new CustomFpsPlayfield();

        protected override PassThroughInputManager CreateInputManager() => new CustomFpsInputManager(Ruleset.RulesetInfo);

        public override DrawableHitObject<CustomFpsHitObject> CreateDrawableRepresentation(CustomFpsHitObject h) => new DrawableCustomFpsHitObject(h);

        private partial class CustomFpsInputManager : RulesetInputManager<CustomFpsAction>
        {
            public CustomFpsInputManager(RulesetInfo ruleset)
                : base(ruleset, 0, SimultaneousBindingMode.Unique)
            {
            }
        }

        private enum CustomFpsAction
        {
            None,
        }
    }
}
