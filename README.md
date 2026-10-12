![icon](assets/icon.png)

# custom-fps

An osu! ruleset that adds a **per-thread custom frame limiter** to the game.

> Originally a patch in osu-plugins osu-patches plugin, then rebuilt natively into a fork of lazer due to osu-plugins blacklisting for unrestricted loading of external code via IL-Weaving; Now reworked into a restricted targeted ruleset providing easier maintenance accross updates while utilizing the osu!framework API, standard ruleset configuration and no IL-Weaving to comply with rules.
> 
>Should comply with rules but use at your own risk, osu-plugins was blacklisted with no player bans so in theory you should be okay to use this even if peppy decides to blacklist in the future.

This patch ruleset was created for myself to resolve an issue with a tiny visible delay between my hand and cursor that genuinely just makes it far less enjoyable to play the game at high star ratings where I enjoy the game. Some other benefits did follow like slightly better audio delay for hitsounds providing a closer to stable feel since stable allowed for running at higher fps which even tho single threaded, could run stably at several thousands of frames per second with modern adaquately built hardware.

I've made it available publicly as to share what is helpful to me, some may only see it as placebo while others may notice an improvement immediately, for a proper test I recommend trying with fps display turned off entirely so you can't tell if it's actually engaged or not until you play. My understanding behind it is that even tho the decrease in delay is small, we are still shortening the overall chain of delay as this is not a perfect world where everything syncs up perfectly and brute-force fps will always reign supreme. Personally I only run higher fps on my non-render threads as my gpu is not the best but I still get a far more responsive game due to the frame being rendered by the gpu being newer a majority of the time compared to even the 1ms you get at 1k fps.

A common misconception I often see is, There is no way a human can tell the difference because humans can't react that fast. But this is fortunately not true, because reactions are not what is being aided by a higher fps at these numbers, but rather the consistency of everything, as reducing the time between frames by running at a higher fps reduces the highest possible age of information between threads and the system. The goal being to reduce the overall impact of the medium to your ability to play the game, allowing only raw skill to dictate your performance in game with supporting hardware. But sadly some people are also not built to perceive these small differences in time frames and genuinely think play at even 4ms is fine which is genuinely a blessing as those of us who can easily do so are forever chasing a less restrictive feeling.

My philosophy is, educate the user about the risks, how they should approach the tool, maybe even hide it behind more technical barriers to entry like environment variables but allow the user the freedom to choose.

## Installation

1. Build the project (`dotnet build`) or grab a [release](https://github.com/TheFantasticLoki/lazer-custom-fps-ruleset/releases) DLL.
2. Drop `osu.Game.Rulesets.CustomFps.dll` into your osu! `rulesets` folder (Settings → General → Open rulesets folder).
3. Restart osu! if it was running.

## How to use

> Options must always be opened at least once after starting the client for ruleset changes to engage due to no startup hook for rulesets.

1. Set **Frame Sync** (Settings → Graphics) to **Basically unlimited**.
2. Open the **Custom frame limiter** section in Settings — the sliders unlock while Frame Sync is on Basically unlimited, and lock again if you switch to any other mode.

| Control | Effect |
| --- | --- |
| **Update thread** | FPS cap for the update (logic) thread — also the source value when syncing. |
| **Sync input and audio to update** | Applies the update limit to the input and audio threads. The draw thread is never synced; stutter there is the most visible. |
| **Input thread** | FPS cap for the input thread (hidden while synced). |
| **Audio thread** | FPS cap for the audio thread (hidden while synced). |
| **Draw thread** | FPS cap for the draw/render thread — always independent. |
| **0 on any slider** | Truly unlimited for that thread. Benchmarking only — see below. |

Your values are stored in the game's database alongside your other settings and survive restarts. Removing the ruleset removes its settings with it.

## ⚠️ Warnings

### Report issues upstream *without* this ruleset first

If you find a bug:

> **Try to reproduce it with this ruleset removed before reporting anything**

Issues that only occur with this ruleset installed will be closed on the official issue tracker. If it still happens without the ruleset, it is a genuine game bug. Report that, and don't mention this project in the report. If the issue does not occur without this ruleset you may open an issue on this repo. Micro-stutters and hitching should be expected if you thoughtlessly try to play at the absolute limits of your hardware and should not be reported on either repos. Set your limits properly.

### Don't play with 0 (unlimited)

A slider set to `0` means that thread runs **completely uncapped**. That will stutter, spike your frametimes, and cook your hardware for no gameplay benefit.

The intended workflow:

1. Set the thread(s) you're testing to `0` and play a demanding map.
2. Watch your frame times and find your **1% lows** (the framerate you dip *to*, not the average).
3. Set your sliders **slightly above** those 1% low values.
4. Play like that daily. That's the whole point of the tool.

## Roadmap

- **Benchmark mode** — measure your own 1% lows in-game and suggest limits automatically.
- **Preset tiers** — default 1000, 2×, 4×, 8× options for the non-draw threads, with custom per-thread override, integrated into a single harmonious mode rather than separate settings fighting each other.
- **Frame Sync integration** — a proper `Custom` frame sync option (like the fork has) instead of piggybacking on `Basically unlimited`, so nothing "overwrites" anything else.
- **Startup application** — apply limits without needing to open settings first (requires a fork-side hook; not possible from a ruleset alone, from current knowledge, will investigate further).

## Compatibility & credits

- Works on official osu! (lazer) — verified against upstream osu!framework public API only.
- The bundled infinity glyph is [Font Awesome Free 5.15.4](https://fontawesome.com) infinity (CC BY 4.0).
- Code was generated by Mimo-v2.5 (AI Disclosure)

## Licence

MIT — see [LICENSE](LICENSE).
