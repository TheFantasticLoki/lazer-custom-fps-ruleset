// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
    /// The settings for this ruleset's update-side frame limiter.
    /// </summary>
    /// <remarks>
    /// This ruleset deliberately leaves the draw/render thread to the game's own Frame Sync limiter (which keeps its
    /// refresh-rate multipliers in Graphics settings) and only overrides the update, input and audio threads. The
    /// wiring runs the first time this panel is opened (osu builds ruleset settings sections on first open, not at
    /// game load), so limits apply from then on and persist via the ruleset config.
    ///
    /// While enabled, the limiter re-asserts itself whenever Frame Sync changes: the framework recomputes the update
    /// thread limit from the frame sync mode, and this section's handler is registered after it, so it runs last and
    /// wins. Disabling restores each thread to the value the framework last chose.
    ///
    /// Only long-standing public <see cref="GameHost"/> API is used here (never fork-only members), so this ruleset
    /// loads and functions identically on an official osu! install or a custom build.
    ///
    /// Config bindables retrieved via <c>GetBindable</c> are weakly bound copies; the fields below keep them (and the
    /// event handlers attached to them) alive for the lifetime of this section.
    /// </remarks>
    public partial class CustomFpsSettingsSubsection : RulesetSettingsSubsection
    {
        public CustomFpsSettingsSubsection(CustomFpsRuleset ruleset)
            : base(ruleset)
        {
        }

        private FillFlowContainer customSlidersContainer = null!;

        private Bindable<bool> enabledBindable = null!;

        private Bindable<CustomFpsPreset> presetBindable = null!;

        private Bindable<double> updateLimit = null!;

        private Bindable<double> audioLimit = null!;

        private Bindable<double> inputLimit = null!;

        private Bindable<FrameSync> frameSyncBindable = null!;

        // Framework-chosen values, snapshotted so we can hand control back cleanly when the limiter is turned off.
        private double frameworkUpdateHz;

        private double frameworkAudioHz;

        private double frameworkInputHz;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (CustomFpsRulesetConfigManager)Config;

            enabledBindable = config.GetBindable<bool>(CustomFpsRulesetSetting.Enabled);
            presetBindable = config.GetBindable<CustomFpsPreset>(CustomFpsRulesetSetting.PresetMode);
            inputLimit = config.GetBindable<double>(CustomFpsRulesetSetting.InputThreadLimit);
            audioLimit = config.GetBindable<double>(CustomFpsRulesetSetting.AudioThreadLimit);
            updateLimit = config.GetBindable<double>(CustomFpsRulesetSetting.UpdateThreadLimit);

            Children = new Drawable[]
            {
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current =
                    {
                        Value = new SettingsNote.Data("Overrides the update, input and audio thread limits while enabled. The draw/render thread always stays under the game's own Frame Sync limiter (Graphics settings), which keeps its refresh-rate multipliers.", SettingsNote.Type.Informational),
                    },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Enable update-side limiter",
                    HintText = "While on, the preset or custom limits below override the update, input and audio threads regardless of Frame Sync mode. Turn off to hand control back to the game.",
                    Current = enabledBindable,
                })
                {
                    Keywords = new[] { "enable", "on", "off", "toggle", "limiter" },
                },
                new SettingsEnumDropdown<CustomFpsPreset>
                {
                    LabelText = "Preset",
                    Keywords = new[] { "preset", "tier", "fps", "limit", "quick", "1k", "2x", "4x", "8x", "custom" },
                    Current = presetBindable,
                },
                customSlidersContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new SettingsItemV2(createLimiterSlider("Update thread", updateLimit))
                        {
                            Keywords = new[] { "update", "thread", "fps", "limit", "custom" },
                        },
                        new SettingsItemV2(createLimiterSlider("Audio thread", audioLimit))
                        {
                            Keywords = new[] { "audio", "thread", "fps", "limit", "custom" },
                        },
                        new SettingsItemV2(createLimiterSlider("Input thread", inputLimit))
                        {
                            Keywords = new[] { "input", "thread", "fps", "limit", "custom" },
                        },
                    },
                },
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current =
                    {
                        Value = new SettingsNote.Data("0 means truly unlimited — benchmarking only. Don't play this way: use 0 to find your 1% lows, then set the limits to those numbers for daily play.", SettingsNote.Type.Warning),
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
            };

            frameSyncBindable = host.Dependencies.Get<FrameworkConfigManager>().GetBindable<FrameSync>(FrameworkSetting.FrameSync);

            // Seed the restore targets with the framework's current choices so a disabled limiter never writes zeros.
            frameworkUpdateHz = host.MaximumUpdateHz;
            frameworkAudioHz = host.AudioThread.ActiveHz;
            frameworkInputHz = host.InputThread.ActiveHz;

            enabledBindable.BindValueChanged(_ => onEngagementChanged());
            presetBindable.BindValueChanged(_ => onPresetChanged());
            updateLimit.BindValueChanged(_ => applyLimitsIfEnabled());
            audioLimit.BindValueChanged(_ => applyLimitsIfEnabled());
            inputLimit.BindValueChanged(_ => applyLimitsIfEnabled());
            // Registered after the framework's own frame-sync handler, so this runs last and can re-assert our values
            // on top of the limits the framework just recomputed.
            frameSyncBindable.BindValueChanged(_ => onFrameSyncChanged());

            onEngagementChanged();
        }

        private void onEngagementChanged()
        {
            if (enabledBindable.Value)
            {
                // Snapshot what the framework currently chose so we can hand control back cleanly on disable.
                frameworkUpdateHz = host.MaximumUpdateHz;
                frameworkAudioHz = host.AudioThread.ActiveHz;
                frameworkInputHz = host.InputThread.ActiveHz;

                applyLimits();
            }
            else
            {
                release();
            }

            updateSlidersVisibility();
        }

        private void onPresetChanged()
        {
            // Choosing a preset writes its rate into the per-thread sliders (a no-op while they're disabled, which is
            // fine: the preset itself drives the applied value in that mode anyway).
            if (presetBindable.Value != CustomFpsPreset.Custom)
            {
                double tier = presetToFrameLimit(presetBindable.Value);

                updateLimit.Value = tier;
                audioLimit.Value = tier;
                inputLimit.Value = tier;
            }

            updateSlidersVisibility();
            applyLimitsIfEnabled();
        }

        private void onFrameSyncChanged()
        {
            if (!enabledBindable.Value)
                return;

            // The framework just recomputed the update limit for the new mode; remember it as the value to restore on
            // disable, then re-assert our own on top.
            frameworkUpdateHz = host.MaximumUpdateHz;
            applyLimits();
        }

        private void applyLimitsIfEnabled()
        {
            if (enabledBindable.Value)
                applyLimits();
        }

        private void applyLimits()
        {
            // In Custom mode the per-thread sliders decide; otherwise the selected tier drives all three equally.
            (double update, double audio, double input) = effectiveLimits();

            // Update first (mutating it can schedule input-thread work), then audio, then input last.
            host.MaximumUpdateHz = limitOrUnlimited(update);
            host.AudioThread.ActiveHz = limitOrUnlimited(audio);
            host.InputThread.ActiveHz = limitOrUnlimited(input);
        }

        private void release()
        {
            host.MaximumUpdateHz = frameworkUpdateHz;
            host.AudioThread.ActiveHz = frameworkAudioHz;
            host.InputThread.ActiveHz = frameworkInputHz;
        }

        private (double update, double audio, double input) effectiveLimits()
        {
            if (presetBindable.Value != CustomFpsPreset.Custom)
            {
                double tier = presetToFrameLimit(presetBindable.Value);
                return (tier, tier, tier);
            }

            return (updateLimit.Value, audioLimit.Value, inputLimit.Value);
        }

        private void updateSlidersVisibility()
        {
            bool custom = presetBindable.Value == CustomFpsPreset.Custom;

            if (custom)
                customSlidersContainer.AutoSizeAxes |= Axes.Y;
            else
            {
                customSlidersContainer.AutoSizeAxes &= ~Axes.Y;
                customSlidersContainer.Height = 0;
            }

            bool editable = enabledBindable.Value;

            updateLimit.Disabled = !editable;
            audioLimit.Disabled = !editable;
            inputLimit.Disabled = !editable;
        }

        private static double presetToFrameLimit(CustomFpsPreset preset) => preset switch
        {
            CustomFpsPreset.Tier1k => 1000d,
            CustomFpsPreset.Tier2x => 2000d,
            CustomFpsPreset.Tier4x => 4000d,
            CustomFpsPreset.Tier8x => 8000d,
            _ => 0d,
        };

        private static double limitOrUnlimited(double value) => value <= 0 ? double.MaxValue : value;

        private static FormSliderBar<double> createLimiterSlider(string caption, Bindable<double> bindable) => new FormSliderBar<double>
        {
            Caption = caption,
            KeyboardStep = 50,
            TooltipFormat = v => v == 0 ? "Unlimited" : $"{v:0} FPS",
            TransferValueOnCommit = true,
            Current = { BindTarget = bindable },
        };
    }
}
