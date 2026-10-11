// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
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
    /// While enabled, it overrides the update, input and audio thread limits regardless of the game's frame sync mode
    /// (the draw thread is always left to the game's own frame limiter). The chosen limits persist here and are
    /// re-applied the next time the settings panel opens.
    /// </remarks>
    public class CustomFpsRulesetConfigManager : RulesetConfigManager<CustomFpsRulesetSetting>
    {
        public CustomFpsRulesetConfigManager(SettingsStore settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            SetDefault(CustomFpsRulesetSetting.Enabled, false);
            SetDefault(CustomFpsRulesetSetting.PresetMode, CustomFpsPreset.Custom);

            SetDefault(CustomFpsRulesetSetting.InputThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.AudioThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.UpdateThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
        }
    }

    public enum CustomFpsRulesetSetting
    {
        /// <summary>
        /// Whether the update-side limiter is active. While active it overrides the update, input and audio thread
        /// limits; the draw thread is always left to the game's own frame limiter.
        /// </summary>
        Enabled,

        /// <summary>
        /// Which preset drives the update-side limits. <see cref="CustomFpsPreset.Custom"/> uses the per-thread
        /// sliders; any other value uses that tier's fixed rate for all three threads.
        /// </summary>
        PresetMode,

        /// <summary>
        /// Frame rate limit for the update thread, in FPS. 0 is treated as unlimited. Only used in Custom mode.
        /// </summary>
        UpdateThreadLimit,

        /// <summary>
        /// Frame rate limit for the audio thread, in FPS. 0 is treated as unlimited. Only used in Custom mode.
        /// </summary>
        AudioThreadLimit,

        /// <summary>
        /// Frame rate limit for the input thread, in FPS. 0 is treated as unlimited. Only used in Custom mode.
        /// </summary>
        InputThreadLimit,
    }

    /// <summary>
    /// Preset tiers for the update-side limiter. The draw thread is never part of a preset — it is always governed by
    /// the game's own frame limiter, which keeps its refresh-rate multipliers.
    /// </summary>
    public enum CustomFpsPreset
    {
        [Description("Custom")]
        Custom,

        [Description("1k (1000 FPS)")]
        Tier1k,

        [Description("2× (2000 FPS)")]
        Tier2x,

        [Description("4× (4000 FPS)")]
        Tier4x,

        [Description("8× (8000 FPS)")]
        Tier8x,
    }
}