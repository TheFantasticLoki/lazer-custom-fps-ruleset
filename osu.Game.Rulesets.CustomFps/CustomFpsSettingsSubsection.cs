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
    /// The render thread can either follow the game's Frame Sync mode (including its refresh-rate multipliers) or use
    /// a custom draw-thread limit. Separately, the update-side limiter controls update, input and audio limits.
    /// Wiring runs the first time this panel is opened (osu builds ruleset settings sections on first open, not at
    /// game load), so limits apply from then on and persist via the ruleset config.
    ///
    /// While either custom limiter is active, it re-asserts itself whenever Frame Sync, execution mode, or display
    /// mode changes. The framework handlers run first; this section records the new framework limits and then applies
    /// any selected custom limits. Switching back to Frame Sync or disabling the update-side limiter restores the
    /// corresponding framework-selected values.
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

        private FillFlowContainer customDrawSliderContainer = null!;

        private Bindable<bool> enabledBindable = null!;

        private Bindable<CustomFpsPreset> presetBindable = null!;

        private Bindable<CustomFpsRenderMode> renderModeBindable = null!;

        private Bindable<double> updateLimit = null!;

        private Bindable<double> audioLimit = null!;

        private Bindable<double> inputLimit = null!;

        private Bindable<double> drawLimit = null!;

        private Bindable<FrameSync> frameSyncBindable = null!;

        private Bindable<ExecutionMode> executionModeBindable = null!;

        private SettingsNote unlimitedWarningNote = null!;

        // Framework-chosen values, snapshotted so we can hand control back cleanly when the limiter is turned off.
        private double frameworkUpdateHz;

        private double frameworkAudioHz;

        private double frameworkInputHz;

        private double frameworkDrawHz;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (CustomFpsRulesetConfigManager)Config;

            enabledBindable = config.GetBindable<bool>(CustomFpsRulesetSetting.Enabled);
            presetBindable = config.GetBindable<CustomFpsPreset>(CustomFpsRulesetSetting.PresetMode);
            renderModeBindable = config.GetBindable<CustomFpsRenderMode>(CustomFpsRulesetSetting.RenderMode);
            inputLimit = config.GetBindable<double>(CustomFpsRulesetSetting.InputThreadLimit);
            audioLimit = config.GetBindable<double>(CustomFpsRulesetSetting.AudioThreadLimit);
            updateLimit = config.GetBindable<double>(CustomFpsRulesetSetting.UpdateThreadLimit);
            drawLimit = config.GetBindable<double>(CustomFpsRulesetSetting.DrawThreadLimit);

            Children = new Drawable[]
            {
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current =
                    {
                        Value = new SettingsNote.Data("Rendering can follow the game's Frame Sync modes or use a custom draw limit. The separate update-side limiter controls update, input and audio.", SettingsNote.Type.Informational),
                    },
                },
                new SettingsEnumDropdown<CustomFpsRenderMode>
                {
                    LabelText = "Render thread limiter",
                    Keywords = new[] { "render", "draw", "frame sync", "refresh rate", "custom" },
                    Current = renderModeBindable,
                },
                customDrawSliderContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new SettingsItemV2(createLimiterSlider("Draw thread", drawLimit))
                        {
                            Keywords = new[] { "draw", "render", "thread", "fps", "limit", "custom" },
                        },
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
                unlimitedWarningNote = new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
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
                        Value = new SettingsNote.Data("Reporting an issue? First reproduce it with this ruleset removed — only reports that also occur on official lazer without it will be looked at upstream.", SettingsNote.Type.Warning),
                    },
                },
            };

            frameSyncBindable = host.Dependencies.Get<FrameworkConfigManager>().GetBindable<FrameSync>(FrameworkSetting.FrameSync);
            executionModeBindable = host.Dependencies.Get<FrameworkConfigManager>().GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode);

            // Seed restore targets with the framework's current choices so custom modes can hand control back cleanly.
            frameworkUpdateHz = host.MaximumUpdateHz;
            frameworkAudioHz = host.AudioThread.ActiveHz;
            frameworkInputHz = host.InputThread.ActiveHz;
            frameworkDrawHz = host.MaximumDrawHz;

            enabledBindable.BindValueChanged(_ => onEngagementChanged());
            presetBindable.BindValueChanged(_ => onPresetChanged());
            updateLimit.BindValueChanged(_ => onUpdateLimitChanged());
            audioLimit.BindValueChanged(_ => onUpdateLimitChanged());
            inputLimit.BindValueChanged(_ => onUpdateLimitChanged());
            drawLimit.BindValueChanged(_ => applyDrawLimitIfCustom());
            renderModeBindable.BindValueChanged(_ => onRenderModeChanged());
            // These handlers are registered after the framework's own handlers, so the latest framework-selected
            // values can be captured before custom limits are re-applied.
            frameSyncBindable.BindValueChanged(_ => onFrameworkLimitsChanged());
            executionModeBindable.BindValueChanged(_ => onExecutionModeChanged());
            host.Window.CurrentDisplayMode.BindValueChanged(_ => onFrameworkLimitsChanged());

            onEngagementChanged();
            onRenderModeChanged();
        }

        private void onEngagementChanged()
        {
            if (enabledBindable.Value)
            {
                // Snapshot only the update-side framework values; render has its own independent mode.
                frameworkUpdateHz = host.MaximumUpdateHz;
                frameworkAudioHz = host.AudioThread.ActiveHz;
                frameworkInputHz = host.InputThread.ActiveHz;

                applyLimits();
            }
            else
            {
                release();
            }

            updateVisibility();
            updateUnlimitedWarning();
        }

        private void onPresetChanged()
        {
            // Presets are an independent mode and must not overwrite the user's saved Custom slider values.
            applyLimitsIfEnabled();
            updateVisibility();
            updateUnlimitedWarning();
        }

        private void onUpdateLimitChanged()
        {
            applyLimitsIfEnabled();
            updateUnlimitedWarning();
        }

        private void onRenderModeChanged()
        {
            if (renderModeBindable.Value == CustomFpsRenderMode.Custom)
            {
                frameworkDrawHz = host.MaximumDrawHz;
                applyDrawLimit();
            }
            else
                host.MaximumDrawHz = frameworkDrawHz;

            updateVisibility();
            updateUnlimitedWarning();
        }

        private void onFrameworkLimitsChanged()
        {
            // The framework has just recomputed these values from Frame Sync, execution mode, or display refresh.
            frameworkUpdateHz = host.MaximumUpdateHz;
            frameworkDrawHz = host.MaximumDrawHz;

            // In single-threaded mode MaximumUpdateHz also changes the input thread's main-loop rate.
            if (executionModeBindable.Value == ExecutionMode.SingleThread)
                frameworkInputHz = host.InputThread.ActiveHz;

            reapplyCustomLimits();
        }

        private void onExecutionModeChanged()
        {
            // Switching execution modes resets the input thread's main-loop rate as well as the framework update rate.
            frameworkUpdateHz = host.MaximumUpdateHz;
            frameworkInputHz = host.InputThread.ActiveHz;
            frameworkDrawHz = host.MaximumDrawHz;

            reapplyCustomLimits();
        }

        private void reapplyCustomLimits()
        {
            applyLimitsIfEnabled();

            if (renderModeBindable.Value == CustomFpsRenderMode.Custom)
                applyDrawLimit();
        }

        private void applyLimitsIfEnabled()
        {
            if (enabledBindable.Value)
                applyLimits();
        }

        private void applyLimits()
        {
            // In Custom mode the per-thread sliders decide; otherwise the selected preset drives all three equally.
            (double update, double audio, double input) = effectiveLimits();

            // Update first (mutating it can schedule input-thread work), then audio, then input last.
            host.MaximumUpdateHz = limitOrUnlimited(update);
            host.AudioThread.ActiveHz = limitOrUnlimited(audio);
            host.InputThread.ActiveHz = limitOrUnlimited(input);
        }

        private void applyDrawLimitIfCustom()
        {
            if (renderModeBindable.Value == CustomFpsRenderMode.Custom)
            {
                applyDrawLimit();
                updateUnlimitedWarning();
            }
        }

        private void applyDrawLimit() => host.MaximumDrawHz = limitOrUnlimited(drawLimit.Value);

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

        private void updateVisibility()
        {
            bool customUpdate = presetBindable.Value == CustomFpsPreset.Custom;

            if (customUpdate)
                customSlidersContainer.AutoSizeAxes |= Axes.Y;
            else
            {
                customSlidersContainer.AutoSizeAxes &= ~Axes.Y;
                customSlidersContainer.Height = 0;
            }

            bool customDraw = renderModeBindable.Value == CustomFpsRenderMode.Custom;

            if (customDraw)
                customDrawSliderContainer.AutoSizeAxes |= Axes.Y;
            else
            {
                customDrawSliderContainer.AutoSizeAxes &= ~Axes.Y;
                customDrawSliderContainer.Height = 0;
            }

            bool updateEditable = enabledBindable.Value && customUpdate;

            updateLimit.Disabled = !updateEditable;
            audioLimit.Disabled = !updateEditable;
            inputLimit.Disabled = !updateEditable;
            drawLimit.Disabled = !customDraw;
        }

        private void updateUnlimitedWarning()
        {
            bool updateUnlimited = enabledBindable.Value
                                   && presetBindable.Value == CustomFpsPreset.Custom
                                   && (updateLimit.Value <= 0 || audioLimit.Value <= 0 || inputLimit.Value <= 0);
            bool drawUnlimited = renderModeBindable.Value == CustomFpsRenderMode.Custom && drawLimit.Value <= 0;

            unlimitedWarningNote.Current.Value = updateUnlimited || drawUnlimited
                ? new SettingsNote.Data("An active thread is set to truly unlimited (0). Use this only for brief benchmarking; it can cause stutter and high power use.", SettingsNote.Type.Critical)
                : null;
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
            HintText = "0 means truly unlimited; use only for brief benchmarking.",
            KeyboardStep = 50,
            TooltipFormat = v => v == 0 ? "Unlimited" : $"{v:0} FPS",
            TransferValueOnCommit = true,
            Current = { BindTarget = bindable },
        };
    }
}
