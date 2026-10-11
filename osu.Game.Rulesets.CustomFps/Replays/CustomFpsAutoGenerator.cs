// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets.CustomFps.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.CustomFps.Replays
{
    public class CustomFpsAutoGenerator : AutoGenerator<CustomFpsReplayFrame>
    {
        public new Beatmap<CustomFpsHitObject> Beatmap => (Beatmap<CustomFpsHitObject>)base.Beatmap;

        public CustomFpsAutoGenerator(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        protected override void GenerateFrames()
        {
            Frames.Add(new CustomFpsReplayFrame());

            foreach (CustomFpsHitObject hitObject in Beatmap.HitObjects)
            {
                Frames.Add(new CustomFpsReplayFrame
                {
                    Time = hitObject.StartTime,
                    Position = hitObject.Position,
                });
            }
        }
    }
}
