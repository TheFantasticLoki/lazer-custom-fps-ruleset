// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Configuration;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets.CustomFps.Configuration
{
    /// <summary>
    /// Stores this ruleset's per-thread frame limiter settings.
    /// </summary>
    /// <remarks>
    /// Uses the standard ruleset config mechanism, which stores values as databased rows rather than in a shared ini
    /// file, so official builds and other rulesets can never collide with or corrupt these settings. Removing the
    /// ruleset simply removes its settings along with it.
    ///
    /// The limiter engages whenever the game's frame sync mode is "Basically unlimited"; the chosen limits persist
    /// here and are re-applied on the next launch.
    /// </remarks>
    public class CustomFpsRulesetConfigManager : RulesetConfigManager<CustomFpsRulesetSetting>
    {
        public CustomFpsRulesetConfigManager(SettingsStore settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            SetDefault(CustomFpsRulesetSetting.InputThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.AudioThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.UpdateThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.DrawThreadLimit, 1000.0, 0.0, 8000.0, 1.0);

            SetDefault(CustomFpsRulesetSetting.SyncThreadLimits, false);
        }
    }

    public enum CustomFpsRulesetSetting
    {
        /// <summary>
        /// Frame rate limit for the update thread, in FPS. 0 is treated as unlimited.
        /// </summary>
        UpdateThreadLimit,

        /// <summary>
        /// Frame rate limit for the audio thread, in FPS. 0 is treated as unlimited.
        /// </summary>
        AudioThreadLimit,

        /// <summary>
        /// Frame rate limit for the input thread, in FPS. 0 is treated as unlimited.
        /// </summary>
        InputThreadLimit,

        /// <summary>
        /// Frame rate limit for the draw thread, in FPS. 0 is treated as unlimited.
        /// </summary>
        DrawThreadLimit,

        /// <summary>
        /// Whether the input and audio threads should follow the update thread limit. The draw thread is never
        /// synced, as stutter there is the most visible to the player.
        /// </summary>
        SyncThreadLimits,
    }
}