// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.CustomFps.Configuration;
using osuTK;

namespace osu.Game.Rulesets.CustomFps
{
    /// <summary>
    /// The settings for this ruleset's per-thread frame limiter.
    /// </summary>
    /// <remarks>
    /// The host limiter is wired up the first time this settings panel is opened (osu only builds ruleset settings
    /// sections on first open, not at game load). osu gives rulesets no startup hook — the only ruleset code that
    /// runs at launch is <c>CreateInstance</c>/<c>CreateConfig</c>, which is handed no host — so limits cannot be
    /// applied at cold boot from a ruleset alone; they persist here and re-apply the next time this panel opens (or
    /// at startup on custom builds that apply ruleset configs themselves). An uninstalled ruleset never gets
    /// constructed, so nothing here can linger or conflict once the ruleset is removed.
    ///
    /// Only long-standing public <see cref="GameHost"/> API is used here (never fork-only members), so this ruleset
    /// loads and functions identically whether it is dropped into an official osu! install or a custom build.
    ///
    /// Config bindables retrieved via <c>GetBindable</c> are weakly bound copies; the fields below keep them (and
    /// the event handlers attached to them) alive for the lifetime of this section.
    /// </remarks>
    public partial class CustomFpsSettingsSubsection : RulesetSettingsSubsection
    {
        public CustomFpsSettingsSubsection(CustomFpsRuleset ruleset)
            : base(ruleset)
        {
        }

        private FillFlowContainer syncedThreadsContainer = null!;

        private readonly List<Bindable<double>> limitBindables = new();

        private Bindable<bool> syncBindable = null!;

        private Bindable<FrameSync> frameSyncBindable = null!;

        private SettingsNote unlimitedWarningNote = null!;

        /// <summary>
        /// UI-only convenience mirroring the current non-draw limits; the realm-stored limit values remain the single
        /// source of truth, so this is derived from them and never persisted.
        /// </summary>
        private readonly Bindable<CustomFpsPreset> preset = new Bindable<CustomFpsPreset>(CustomFpsPreset.Custom);

        private bool updatingPresetFromSliders;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (CustomFpsRulesetConfigManager)Config;

            syncBindable = config.GetBindable<bool>(CustomFpsRulesetSetting.SyncThreadLimits);

            limitBindables.Add(config.GetBindable<double>(CustomFpsRulesetSetting.InputThreadLimit));
            limitBindables.Add(config.GetBindable<double>(CustomFpsRulesetSetting.AudioThreadLimit));
            limitBindables.Add(config.GetBindable<double>(CustomFpsRulesetSetting.UpdateThreadLimit));
            limitBindables.Add(config.GetBindable<double>(CustomFpsRulesetSetting.DrawThreadLimit));

            var sync = syncBindable;
            var input = limitBindables[0];
            var audio = limitBindables[1];
            var update = limitBindables[2];
            var draw = limitBindables[3];

            Children = new Drawable[]
            {
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current =
                    {
                        Value = new SettingsNote.Data("Takes over while Frame Sync (Graphics settings) is set to Basically unlimited. Any other mode hands control back to the game and locks the sliders.", SettingsNote.Type.Informational),
                    },
                },
                unlimitedWarningNote = new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                },
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current =
                    {
                        Value = new SettingsNote.Data("0 means truly unlimited — benchmarking only. Don't play this way: use 0 to find your 1% lows, then set the sliders to those numbers for daily play.", SettingsNote.Type.Warning),
                    },
                },
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current =
                    {
                        Value = new SettingsNote.Data("Reporting an issue? First reproduce it with this ruleset removed — only reports that also occur on official lazer without it will be looked at upstream.", SettingsNote.Type.Warning),
                    },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Sync input and audio to update",
                    HintText = "Applies the update thread limit to the input and audio threads. The draw thread always keeps its own limit.",
                    Current = sync,
                })
                {
                    Keywords = new[] { "sync", "threads", "limit", "all" },
                },
                new SettingsEnumDropdown<CustomFpsPreset>
                {
                    LabelText = "Preset",
                    Keywords = new[] { "preset", "tier", "fps", "limit", "quick", "1k", "2x", "4x", "8x" },
                    Current = preset,
                },
                new SettingsItemV2(createLimiterSlider("Update thread", update))
                {
                    Keywords = new[] { "update", "thread", "fps", "limit", "custom" },
                },
                syncedThreadsContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new SettingsItemV2(createLimiterSlider("Input thread", input))
                        {
                            Keywords = new[] { "input", "thread", "fps", "limit", "custom" },
                        },
                        new SettingsItemV2(createLimiterSlider("Audio thread", audio))
                        {
                            Keywords = new[] { "audio", "thread", "fps", "limit", "custom" },
                        },
                    },
                },
                new SettingsItemV2(createLimiterSlider("Draw thread", draw))
                {
                    Keywords = new[] { "draw", "thread", "fps", "limit", "custom" },
                },
            };

            // Observe the game's own frame sync mode: the one fork-agnostic signal that tells us when the framework
            // is (or is not) applying its own limiter, so we can take over exactly while "Basically unlimited".
            frameSyncBindable = host.Dependencies.Get<FrameworkConfigManager>().GetBindable<FrameSync>(FrameworkSetting.FrameSync);

            var frameSync = frameSyncBindable;

            frameSync.BindValueChanged(_ =>
            {
                updateSliderAvailability();

                // When frame sync changes to any other mode, the framework has already applied the correct limits
                // for it (its own handler runs before ours), so re-asserting is only needed on engagement.
                if (frameSync.Value == FrameSync.Unlimited)
                    applyLimits();
            }, true);

            // Re-apply whenever an input changes while engaged (the sliders, or a sync toggle), and flag any
            // slider parked at truly-unlimited while engaged.
            input.BindValueChanged(_ => onLimitsChanged());
            audio.BindValueChanged(_ => onLimitsChanged());
            update.BindValueChanged(_ => onLimitsChanged());
            draw.BindValueChanged(_ => onLimitsChanged());
            sync.BindValueChanged(_ => updateSyncedThreadsVisibility(), true);

            // Selecting a preset drives the non-draw sliders together (draw keeps its own). Moving any slider by hand
            // flips the preset back to Custom via updatePresetFromSliders, so the two never fight — the realm-stored
            // limit values stay the single source of truth.
            preset.BindValueChanged(p =>
            {
                if (updatingPresetFromSliders)
                    return;

                if (presetToFrameLimit(p.NewValue) is not { } limit)
                    return;

                update.Value = limit;
                input.Value = limit;
                audio.Value = limit;
            });

            // Seed the dropdown from the persisted limits on open.
            updatePresetFromSliders();

            void onLimitsChanged()
            {
                applyLimitsIfEngaged();
                updateUnlimitedWarning();
                updatePresetFromSliders();
            }

            void updatePresetFromSliders()
            {
                // When synced, the update limit alone drives input and audio, so it alone decides the tier.
                var detected = sync.Value
                    ? frameLimitToPreset(update.Value)
                    : update.Value == input.Value && input.Value == audio.Value
                        ? frameLimitToPreset(update.Value)
                        : CustomFpsPreset.Custom;

                if (detected == preset.Value)
                    return;

                updatingPresetFromSliders = true;
                preset.Value = detected;
                updatingPresetFromSliders = false;
            }

            void updateUnlimitedWarning()
            {
                bool anyUnlimited = new[] { input, audio, update, draw }.Any(b => b.Value == 0);

                unlimitedWarningNote.Current.Value = anyUnlimited
                    ? new SettingsNote.Data("At least one thread is truly unlimited (0) — expect stutter and high power use. Use this only to measure 1% lows, then lock in real limits.", SettingsNote.Type.Critical)
                    : null;
            }

            void applyLimitsIfEngaged()
            {
                if (frameSync.Value == FrameSync.Unlimited)
                    applyLimits();
            }

            void applyLimits()
            {
                // Sync ties the input and audio threads to the update thread; the draw (render) thread is never
                // synced, as stutter there is the most visible to the player.
                host.AudioThread.ActiveHz = limitOrUnlimited(sync.Value ? update.Value : audio.Value);
                host.MaximumDrawHz = limitOrUnlimited(draw.Value);
                host.MaximumUpdateHz = limitOrUnlimited(update.Value);

                // Must be done last — the host schedules a task that resets the input thread limiter when
                // MaximumUpdateHz is mutated.
                host.InputThread.ActiveHz = limitOrUnlimited(sync.Value ? update.Value : input.Value);
            }

            void updateSliderAvailability()
            {
                bool engaged = frameSync.Value == FrameSync.Unlimited;

                foreach (var bindable in new[] { input, audio, update, draw })
                    bindable.Disabled = !engaged;
            }

            void updateSyncedThreadsVisibility()
            {
                if (sync.Value)
                {
                    syncedThreadsContainer.AutoSizeAxes &= ~Axes.Y;
                    syncedThreadsContainer.Height = 0;
                }
                else
                    syncedThreadsContainer.AutoSizeAxes |= Axes.Y;
            }

            updateUnlimitedWarning();

            static double limitOrUnlimited(double value) => value <= 0 ? double.MaxValue : value;

            static double? presetToFrameLimit(CustomFpsPreset preset) => preset switch
            {
                CustomFpsPreset.Tier1k => 1000d,
                CustomFpsPreset.Tier2x => 2000d,
                CustomFpsPreset.Tier4x => 4000d,
                CustomFpsPreset.Tier8x => 8000d,
                _ => null,
            };

            static CustomFpsPreset frameLimitToPreset(double value) => value switch
            {
                1000 => CustomFpsPreset.Tier1k,
                2000 => CustomFpsPreset.Tier2x,
                4000 => CustomFpsPreset.Tier4x,
                8000 => CustomFpsPreset.Tier8x,
                _ => CustomFpsPreset.Custom,
            };

            static FormSliderBar<double> createLimiterSlider(string caption, Bindable<double> bindable) => new FormSliderBar<double>
            {
                Caption = caption,
                KeyboardStep = 50,
                TooltipFormat = v => v == 0 ? "Unlimited" : $"{v:0} FPS",
                TransferValueOnCommit = true,
                Current = { BindTarget = bindable },
            };
        }
    }

    /// <summary>
    /// Convenience groupings for the non-draw (update, input, audio) thread limits. The draw thread always keeps its
    /// own limit and is never part of a preset. These are a UI shortcut only — the underlying limit values remain the
    /// single source of truth, so nothing here is persisted.
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