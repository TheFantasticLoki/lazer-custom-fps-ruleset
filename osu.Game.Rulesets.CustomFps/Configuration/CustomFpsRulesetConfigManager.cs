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
    /// file, so official builds and other rulesets can never collide with or corrupt these settings. If the ruleset
    /// is removed, its settings are no longer active; the database rows may remain and be reused if it is installed
    /// again.
    ///
    /// Update, input and audio limits are controlled by the update-side limiter. The draw thread can either follow the
    /// game's frame sync mode or use its own custom limit. The chosen limits persist here and are re-applied the next
    /// time the settings panel opens.
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
            SetDefault(CustomFpsRulesetSetting.PresetMode, CustomFpsPreset.Tier1k);

            SetDefault(CustomFpsRulesetSetting.InputThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.AudioThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.UpdateThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.DrawThreadLimit, 1000.0, 0.0, 8000.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.RenderMode, CustomFpsRenderMode.UseFrameSync);
            SetDefault(CustomFpsRulesetSetting.CaptureBufferSeconds, 10.0, 0.0, 30.0, 1.0);
            SetDefault(CustomFpsRulesetSetting.SyncInputAudioWithUpdate, false);
        }
    }

    public enum CustomFpsRulesetSetting
    {
        /// <summary>
        /// Whether the update-side limiter is active. While active it overrides the update, input and audio thread
        /// limits. Render-thread limiting is controlled separately by <see cref="RenderMode"/>.
        /// </summary>
        Enabled,

        /// <summary>
        /// Which preset drives the update-side limits. <see cref="CustomFpsPreset.Custom"/> uses the per-thread
        /// sliders, with input and audio optionally following the update limit; any other value uses that tier's
        /// fixed rate for all three threads.
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

        /// <summary>
        /// Frame rate limit for the draw thread, in FPS. 0 is treated as unlimited. Only used in custom render mode.
        /// </summary>
        DrawThreadLimit,

        /// <summary>
        /// Whether the draw thread follows the game's frame sync or uses a custom limit.
        /// </summary>
        RenderMode,

        /// <summary>
        /// Number of seconds to exclude from the start and end of a manual performance capture.
        /// </summary>
        CaptureBufferSeconds,

        /// <summary>
        /// Whether custom input and audio limits follow the custom update-thread limit.
        /// </summary>
        SyncInputAudioWithUpdate,
    }

    /// <summary>
    /// Preset tiers for the update-side limiter. The draw thread is never part of an update preset; it has a separate
    /// render mode that can either follow the game's frame sync (and keep its refresh-rate multipliers) or use a
    /// custom limit.
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

    /// <summary>
    /// Selects whether rendering follows the game's built-in frame sync or uses a custom draw-thread limit.
    /// </summary>
    public enum CustomFpsRenderMode
    {
        [Description("Use Frame Sync")]
        UseFrameSync,

        [Description("Custom")]
        Custom,
    }
}