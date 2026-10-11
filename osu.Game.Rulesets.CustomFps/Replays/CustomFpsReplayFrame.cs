// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.CustomFps.Replays
{
    public class CustomFpsReplayFrame : ReplayFrame
    {
        public List<CustomFpsAction> Actions = new List<CustomFpsAction>();
        public Vector2 Position;

        public CustomFpsReplayFrame(CustomFpsAction? button = null)
        {
            if (button.HasValue)
                Actions.Add(button.Value);
        }

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is CustomFpsReplayFrame fpsFrame && Time == fpsFrame.Time && Position == fpsFrame.Position && Actions.SequenceEqual(fpsFrame.Actions);
    }
}
