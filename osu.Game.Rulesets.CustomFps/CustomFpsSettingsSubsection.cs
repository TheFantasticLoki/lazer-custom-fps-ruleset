// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
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

        private FillFlowContainer customThreadSlidersContainer = null!;

        private FillFlowContainer customDrawSliderContainer = null!;

        private FillFlowContainer sessionMetricsContainer = null!;

        private Bindable<bool> enabledBindable = null!;

        private Bindable<CustomFpsPreset> presetBindable = null!;

        private Bindable<CustomFpsRenderMode> renderModeBindable = null!;

        private Bindable<double> updateLimit = null!;

        private Bindable<double> audioLimit = null!;

        private Bindable<double> inputLimit = null!;

        private Bindable<double> drawLimit = null!;

        private Bindable<double> captureBufferSeconds = null!;

        private Bindable<bool> syncInputAudioWithUpdate = null!;

        private Bindable<FrameSync> frameSyncBindable = null!;

        private Bindable<ExecutionMode> executionModeBindable = null!;

        private SettingsNote unlimitedWarningNote = null!;

        private SettingsNote sessionMetricsNote = null!;

        private SettingsButtonV2 applySuggestedLimitsButton = null!;

        private SettingsButtonV2 collectionButton = null!;

        private SettingsButtonV2 sessionMetricsToggleButton = null!;

        private SettingsButtonV2 clearDataButton = null!;

        private readonly Queue<PerformanceSample> performanceSamples = new Queue<PerformanceSample>();

        private readonly List<PerformanceSample> currentCaptureSamples = new List<PerformanceSample>();

        private bool isCollecting;

        private double captureStartTime;

        private int lastCaptureSamplesRetained = -1;

        private ScheduledDelegate captureSamplingTask = null!;

        private ScheduledDelegate metricsDisplayTask = null!;

        private const double metrics_sample_interval = 250;

        private const double metrics_display_interval = 1000;

        private const int maximum_metrics_samples = 1200;

        private const int minimum_samples_for_suggestion = 120;

        private const int maximum_capture_samples = 12000;

        private struct PerformanceSample
        {
            public double Time;
            public double UpdateFps;
            public double DrawFps;
        }

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
            captureBufferSeconds = config.GetBindable<double>(CustomFpsRulesetSetting.CaptureBufferSeconds);
            syncInputAudioWithUpdate = config.GetBindable<bool>(CustomFpsRulesetSetting.SyncInputAudioWithUpdate);

            Children = new Drawable[]
            {
                new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current =
                    {
                        Value = new SettingsNote.Data("Reporting an issue? First reproduce it with this ruleset removed — only reports that also occur on official lazer without it will be looked at upstream.", SettingsNote.Type.Warning),
                    },
                },
                unlimitedWarningNote = new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                },
                new OsuSpriteText
                {
                    Text = "UPDATE / INPUT / AUDIO",
                    Font = OsuFont.GetFont(size: 20),
                    Margin = new MarginPadding { Vertical = SettingsSubsection.VERTICAL_PADDING, Horizontal = 5 },
                    Padding = SettingsPanel.CONTENT_PADDING,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Enable update-side limiter",
                    HintText = "The preset or custom limits override update, input and audio. Rendering is controlled separately below.",
                    Current = enabledBindable,
                })
                {
                    Keywords = new[] { "enable", "on", "off", "toggle", "limiter" },
                },
                new SettingsEnumDropdown<CustomFpsPreset>
                {
                    LabelText = "Update / input / audio preset",
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
                        new SettingsItemV2(new FormCheckBox
                        {
                            Caption = "Sync input and audio with update",
                            HintText = "Use the update-thread limit for input and audio instead of their saved custom limits.",
                            Current = syncInputAudioWithUpdate,
                        })
                        {
                            Keywords = new[] { "sync", "input", "audio", "update", "thread", "fps", "custom" },
                        },
                        customThreadSlidersContainer = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 5),
                            Masking = true,
                            Children = new Drawable[]
                            {
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
                    },
                },
                new OsuSpriteText
                {
                    Text = "RENDERING",
                    Font = OsuFont.GetFont(size: 20),
                    Margin = new MarginPadding { Vertical = SettingsSubsection.VERTICAL_PADDING, Horizontal = 5 },
                    Padding = SettingsPanel.CONTENT_PADDING,
                },
                new SettingsEnumDropdown<CustomFpsRenderMode>
                {
                    LabelText = "Render thread limit",
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
                sessionMetricsToggleButton = new SettingsButtonV2
                {
                    Text = "Show performance capture",
                    Keywords = new[] { "performance", "measurement", "benchmark", "capture", "fps" },
                    Action = toggleSessionMetrics,
                },
                sessionMetricsContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                    Masking = true,
                    Children = new Drawable[]
                    {
                        sessionMetricsNote = new SettingsNote
                        {
                            RelativeSizeAxes = Axes.X,
                            Current =
                            {
                                Value = new SettingsNote.Data("MANUAL PERFORMANCE CAPTURE", SettingsNote.Type.Informational),
                            },
                        },
                        new SettingsNote
                        {
                            RelativeSizeAxes = Axes.X,
                            Current =
                            {
                                Value = new SettingsNote.Data("Start collection before a play, then stop it after gameplay. A configurable buffer is trimmed from both ends so loading and post-play screens can be excluded.", SettingsNote.Type.Informational),
                            },
                        },
                        collectionButton = new SettingsButtonV2
                        {
                            Text = "Start collecting",
                            Keywords = new[] { "start", "stop", "collect", "capture", "performance", "fps" },
                            Action = toggleCollection,
                        },
                        clearDataButton = new SettingsButtonV2
                        {
                            Text = "Clear collected data",
                            Keywords = new[] { "clear", "reset", "delete", "performance", "fps" },
                            Action = clearCollectedData,
                            Enabled = { Value = false },
                        },
                        new SettingsItemV2(new FormSliderBar<double>
                        {
                            Caption = "Start/end buffer",
                            HintText = "This many seconds are excluded from both the beginning and end when you stop a capture.",
                            KeyboardStep = 1,
                            TooltipFormat = v => $"{v:0} seconds",
                            TransferValueOnCommit = true,
                            Current = { BindTarget = captureBufferSeconds },
                        })
                        {
                            Keywords = new[] { "buffer", "seconds", "trim", "capture", "loading", "results" },
                        },
                        applySuggestedLimitsButton = new SettingsButtonV2
                        {
                            Text = "Apply suggested limits",
                            TooltipText = "Uses 5% below the 1% lows from the manually collected samples.",
                            Keywords = new[] { "suggested", "recommended", "capture", "fps", "limits" },
                            Action = applySuggestedLimits,
                            Enabled = { Value = false },
                        },
                    }
                },
            };

            metricsDisplayTask = host.UpdateThread.Scheduler.AddDelayed(updateSessionMetricsDisplay, metrics_display_interval, repeat: true);
            updateSessionMetricsDisplay();

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
            syncInputAudioWithUpdate.BindValueChanged(_ => onSyncInputAudioChanged());
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

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (isCollecting)
                    stopCollection();

                metricsDisplayTask?.Cancel();
            }

            base.Dispose(isDisposing);
        }

        private void toggleCollection()
        {
            if (isCollecting)
                stopCollection();
            else
                startCollection();
        }

        private void toggleSessionMetrics()
        {
            bool expand = (sessionMetricsContainer.AutoSizeAxes & Axes.Y) == 0;

            if (expand)
            {
                sessionMetricsContainer.AutoSizeAxes |= Axes.Y;
                sessionMetricsToggleButton.Text = "Hide performance capture";
            }
            else
            {
                sessionMetricsContainer.AutoSizeAxes &= ~Axes.Y;
                sessionMetricsContainer.Height = 0;
                sessionMetricsToggleButton.Text = "Show performance capture";
            }
        }

        private void startCollection()
        {
            currentCaptureSamples.Clear();
            captureStartTime = host.UpdateThread.Clock.CurrentTime;
            isCollecting = true;
            collectionButton.Text = "Stop and review";
            clearDataButton.Enabled.Value = false;
            captureSamplingTask = host.UpdateThread.Scheduler.AddDelayed(sampleCapture, metrics_sample_interval, repeat: true);
            updateSessionMetricsDisplay();
        }

        private void stopCollection()
        {
            if (!isCollecting)
                return;

            double captureEndTime = host.UpdateThread.Clock.CurrentTime;
            double trimStart = captureStartTime + captureBufferSeconds.Value * 1000;
            double trimEnd = captureEndTime - captureBufferSeconds.Value * 1000;
            int retainedSampleCount = 0;

            captureSamplingTask?.Cancel();
            captureSamplingTask = null;

            foreach (PerformanceSample sample in currentCaptureSamples)
            {
                if (sample.Time < trimStart || sample.Time > trimEnd)
                    continue;

                performanceSamples.Enqueue(sample);
                if (performanceSamples.Count > maximum_metrics_samples)
                    performanceSamples.Dequeue();

                retainedSampleCount++;
            }

            currentCaptureSamples.Clear();
            isCollecting = false;
            lastCaptureSamplesRetained = retainedSampleCount;
            collectionButton.Text = "Start collecting";
            updateSessionMetricsDisplay();
        }

        private void clearCollectedData()
        {
            if (isCollecting)
                return;

            performanceSamples.Clear();
            lastCaptureSamplesRetained = -1;
            updateSessionMetricsDisplay();
        }

        private void sampleCapture()
        {
            if (!isCollecting || !host.IsActive.Value)
                return;

            double updateFps = host.UpdateThread.Clock.FramesPerSecond;
            double drawFps = host.DrawThread.Clock.FramesPerSecond;

            if (updateFps <= 0 || drawFps <= 0 || double.IsNaN(updateFps) || double.IsNaN(drawFps)
                               || double.IsInfinity(updateFps) || double.IsInfinity(drawFps))
                return;

            currentCaptureSamples.Add(new PerformanceSample
            {
                Time = host.UpdateThread.Clock.CurrentTime,
                UpdateFps = updateFps,
                DrawFps = drawFps,
            });

            if (currentCaptureSamples.Count > maximum_capture_samples)
                currentCaptureSamples.RemoveAt(0);
        }

        private void updateSessionMetricsDisplay()
        {
            string captureStatus = isCollecting
                ? $"COLLECTING ({currentCaptureSamples.Count} samples) — stop after gameplay to review."
                : lastCaptureSamplesRetained < 0
                    ? "NOT COLLECTING — retained readings are unchanged until the next capture."
                    : lastCaptureSamplesRetained == 0
                        ? "LAST CAPTURE: no samples survived the buffer. Try a longer capture or smaller buffer."
                        : $"LAST CAPTURE: {lastCaptureSamplesRetained} samples retained.";

            clearDataButton.Enabled.Value = !isCollecting && performanceSamples.Count > 0;

            if (performanceSamples.Count == 0)
            {
                sessionMetricsNote.Current.Value = new SettingsNote.Data($"NO RETAINED CAPTURE DATA\n{captureStatus}", SettingsNote.Type.Informational);
                applySuggestedLimitsButton.Enabled.Value = false;
                return;
            }

            (double updateMinimum, double updateOnePercentLow) = summarize(performanceSamples.Select(s => s.UpdateFps));
            (double drawMinimum, double drawOnePercentLow) = summarize(performanceSamples.Select(s => s.DrawFps));
            double suggestedUpdateLimit = suggestedLimit(updateOnePercentLow);
            double suggestedDrawLimit = suggestedLimit(drawOnePercentLow);

            string readout = $"RETAINED CAPTURE DATA ({performanceSamples.Count} samples)\n"
                             + $"UPDATE  1% LOW {updateOnePercentLow:0}  |  MIN {updateMinimum:0} FPS\n"
                             + $"RENDER  1% LOW {drawOnePercentLow:0}  |  MIN {drawMinimum:0} FPS\n"
                             + $"SUGGESTED CAPS (-5%): UPDATE {suggestedUpdateLimit:0}  |  RENDER {suggestedDrawLimit:0} FPS\n"
                             + captureStatus;

            sessionMetricsNote.Current.Value = new SettingsNote.Data(readout, SettingsNote.Type.Informational);
            applySuggestedLimitsButton.Enabled.Value = performanceSamples.Count >= minimum_samples_for_suggestion;
        }

        private void applySuggestedLimits()
        {
            if (performanceSamples.Count < minimum_samples_for_suggestion)
                return;

            double updateOnePercentLow = summarize(performanceSamples.Select(s => s.UpdateFps)).onePercentLow;
            double drawOnePercentLow = summarize(performanceSamples.Select(s => s.DrawFps)).onePercentLow;

            enabledBindable.Value = true;
            presetBindable.Value = CustomFpsPreset.Custom;
            renderModeBindable.Value = CustomFpsRenderMode.Custom;
            updateLimit.Value = suggestedLimit(updateOnePercentLow);
            drawLimit.Value = suggestedLimit(drawOnePercentLow);
        }

        private static (double minimum, double onePercentLow) summarize(IEnumerable<double> samples)
        {
            double[] ordered = samples.OrderBy(fps => fps).ToArray();
            int onePercentLowIndex = Math.Max(0, (int)Math.Ceiling(ordered.Length * 0.01) - 1);

            return (ordered[0], ordered[onePercentLowIndex]);
        }

        private static double suggestedLimit(double onePercentLow) => Math.Clamp(Math.Floor(onePercentLow * 0.95), 1, 8000);

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

        private void onSyncInputAudioChanged()
        {
            applyLimitsIfEnabled();
            updateVisibility();
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
            // In Custom mode, input and audio either use their own sliders or follow the update limit.
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

            return syncInputAudioWithUpdate.Value
                ? (updateLimit.Value, updateLimit.Value, updateLimit.Value)
                : (updateLimit.Value, audioLimit.Value, inputLimit.Value);
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
            bool customThreadLimitsVisible = customUpdate && !syncInputAudioWithUpdate.Value;

            if (customThreadLimitsVisible)
                customThreadSlidersContainer.AutoSizeAxes |= Axes.Y;
            else
            {
                customThreadSlidersContainer.AutoSizeAxes &= ~Axes.Y;
                customThreadSlidersContainer.Height = 0;
            }

            audioLimit.Disabled = !updateEditable || syncInputAudioWithUpdate.Value;
            inputLimit.Disabled = !updateEditable || syncInputAudioWithUpdate.Value;
            drawLimit.Disabled = !customDraw;
        }

        private void updateUnlimitedWarning()
        {
            bool updateUnlimited = enabledBindable.Value
                                   && presetBindable.Value == CustomFpsPreset.Custom
                                   && (updateLimit.Value <= 0
                                       || (!syncInputAudioWithUpdate.Value && (audioLimit.Value <= 0 || inputLimit.Value <= 0)));
            bool drawUnlimited = renderModeBindable.Value == CustomFpsRenderMode.Custom && drawLimit.Value <= 0;

            unlimitedWarningNote.Current.Value = updateUnlimited || drawUnlimited
                ? new SettingsNote.Data("An active thread is set to truly unlimited (0). Use this only for brief manual testing; it can cause stutter and high power use.", SettingsNote.Type.Critical)
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
            HintText = "0 means truly unlimited; use only for brief manual testing.",
            KeyboardStep = 50,
            TooltipFormat = v => v == 0 ? "Unlimited" : $"{v:0} FPS",
            TransferValueOnCommit = true,
            Current = { BindTarget = bindable },
        };
    }
}
