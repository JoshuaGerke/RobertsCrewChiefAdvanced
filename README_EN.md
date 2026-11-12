# RRCA – AI crew for Crew Chief · v0.9 Alpha 3

Illustrated step-by-step guide: `GUIDE_RRCA_EN.pdf` in the same folder.

Unofficial free test build for racing simulations, based on Crew Chief V4 by Britton IT Ltd. Ben handles strategy and race observation, Mik the car, Sara immediate side warnings. No affiliation with the named projects, simulators or voice providers.

**Alpha software: calls may be incorrect, late or missing. Simulation use only; not for real vehicles or safety-critical decisions.** The driver remains responsible. Free download does not include free AI/voice services. This is a test candidate, not a fully legally cleared public release; see `FREEWARE_PRUEFUNG.md`.

## Install and set up

1. Extract into a new writable folder; keep a separate backup of your working installation. Run `CrewChiefV4.exe`. Requires Windows x64, .NET Framework 4.8 and Edge WebView2 Runtime. These are not silently installed.
2. The **public test ZIP** sets `RRCAStandaloneData=true` in `CrewChiefV4.exe.config`, using separate profiles and sounds without importing an original Crew Chief profile. Do not extract over the original installation.
3. Download Crew Chief base sounds to the new sound folder using the built-in sound manager if needed. Audio packs are not included. Sound downloads have separate rights and may be large.
4. Under **Team & voices → Services & keys**, save your own OpenRouter and ElevenLabs keys, test the connection and check your provider's model availability, quota and prices.
5. Load the voice list, choose your own German-capable voices for all three members and preview while stopped. No author's voice selections ship as new defaults. Prepare radio clips, generate Sara's spotter pack, then fully close and reopen the app. Use **Fix and restart** while stopped when offered. Sara's loaded pack must match her selected voice, model and radio filter.
6. Choose the microphone. Assign the controller action **KI-Team anfunken** in the Crew Chief window. Hold, speak, release. Test typed questions while stopped. Do not change settings during driving.
7. Enter your name/nickname in **AI & radio** and Apply with the crew stopped. Select the simulator or enable automatic detection. Configure its required shared memory, UDP or plugin separately. A detected process is not proof of valid driving telemetry.

No automatic Windows substitute voice is used. If the selected voice fails, a call may be missing. Prepared Sara clips work locally without a live cloud connection. A readiness warning blocks startup when the required pack is missing or not loaded. Members can be switched separately; **Sara only** disables Ben and Mik. Disabling Sara removes her side warnings.

## Language, help and radio schedule

Choose **Deutsch / English / Español** at the top. This changes the new RRCA interface and **?** help, stored in the local WebView2 profile. **Spoken race radio, Sara clips and the classic Crew Chief interface do not automatically change to English/Spanish.** Provider/technical errors can retain their original language.

Messages and Background are adjustable directly in the cockpit. Background means radio ambience, not game audio. Change models, voices, microphones and report settings while stopped. Ten scheduled topics supplement independent event recognition. Each has On/Off, base seconds and random earlier/later tolerance: 120 −7/+8 schedules 113–128 seconds. Airtime is not guaranteed; urgent calls, corner workload, confirmed qualifying push laps and missing data defer reports. Routine calls are at least 20 seconds apart. Increasing frequency cannot create unavailable telemetry.

## Privacy and costs

AI conversations send relevant racing facts, your question and possibly selected memories to your configured provider. OpenRouter may forward these to the selected model provider; its policies also apply. See [OpenRouter privacy](https://openrouter.ai/privacy). Push-to-talk sends the recorded speech segment to cloud transcription; no continuous/full microphone recording is stored. TTS sends the spoken text to your voice provider.

**Send driver names to AI** defaults off for new profiles. This hides opponent names in AI requests, but your own radio name, typed questions and TTS text can still contain names. It is not full anonymisation. Keys are protected by Windows DPAPI for your Windows user, and sent to the respective provider for authentication. Never distribute keys, profiles, screenshots containing secrets or private logs.

Persistent memory stores text/preferences and bounded historical facts outside the ZIP. It does not train model weights, change code or replace current measurements. Relevant notes may be sent in AI conversations. Optional local JSONL diagnostics are bounded to 128 MiB per capture and 512 MiB total, sampling up to 20 Hz. No keys, authentication headers or raw microphone recordings; logs can contain names and radio text. Review them before sharing. Software timestamps do not measure physical audible latency.

Use your own provider accounts and authorised voices. Check [ElevenLabs terms](https://elevenlabs.io/terms-of-use-eu), applicable voice rights and integration terms. No API credit or third-party usage rights are supplied.

## Files and directories

“Documents” means the Windows known folder, including redirected/OneDrive paths. Extraction alone does not create these folders; each function creates its own data when used.

| Function | Public ZIP location |
|---|---|
| Profile initialisation | `Documents\RRCA\CrewChiefV4\Profiles` |
| Controller settings/names | `…\Profiles\ControllerData\ButtonNames` |
| Console/error/debug logs | `Documents\RRCA\CrewChiefV4\DebugLogs` |
| Bounded AI radio log, when enabled | `Documents\RRCA\CrewChiefV4\AiTeam\Logs` |
| Runtime data/sound downloads | `%LOCALAPPDATA%\RobertsCrewChiefAdvanced` and `…\Sounds` |
| Generated Sara pack | `…\Sounds\voice\spotter_Sara` plus Sara radio-check/acknowledgement folders |
| RRCA interface/language/browser cache | `%LOCALAPPDATA%\RCCA\WebView2` |
| .NET per-user configuration | `%LOCALAPPDATA%\Britton_IT_Ltd\…\user.config` (EXE location hash/version dependent) |
| Keys, only after saving | `%APPDATA%\RobertsCrewChiefAdvanced\api_key.bin`, `elevenlabs_key.bin` |
| Prepared AI voice cache | `%APPDATA%\RobertsCrewChiefAdvanced\stimmen-cache\*.pcm` |
| Memory, when enabled | `Documents\RRCA\Fahrergedaechtnis`: `notizen.txt`, `Gespraeche\*.jsonl`, `telemetrie-fakten-v1.json` and backup |
| Diagnostics, when enabled | `Documents\CrewChief20\RCCA-LiveDiagnostics\*.jsonl` |
| Optional track/curve features | Profile base subfolders `Track_landmarks`, `Track_splines`, `Pace_notes`, `RF2`, `LMU`; AI curve profiles `%APPDATA%\RobertsCrewChiefAdvanced\kurven` |
| Sara generation staging | Windows temp `rcca_sara_<random ID>` |

**Existing Crew Chief installation:** public mode does not automatically import/read `Documents\CrewChiefV4` or `%LOCALAPPDATA%\CrewChiefV4\Sounds`. Existing RRCA keys, memory and UI language for the same Windows user are intentionally reused; this does not mean they were included in the ZIP. If you explicitly select a shared sound-folder override, sound/Sara operations can write there. Keep the new folder for separation.

Earlier private builds with `RRCAStandaloneData=false` keep using the original Crew Chief paths. Do not confuse the two modes. Update your RRCA folder or extract a separate build; profiles, keys and memory remain external. Public mode prevents the upstream Crew Chief binary updater from replacing the fork; obtain RRCA updates deliberately from its project. Sound updates are separate. Removing the app never automatically removes your personal data. Do not blindly remove shared folders.

## Validation, limitations and support

See `TESTSTATUS_ALPHA3.md` for executed checks. This build needs a live driving test. Today's RaceRoom observation used Alpha 1; Alpha 3 incorporates the findings. AMS2 and other adapters need separate live testing. RRCA does not load another installed PitCrew catalogue. The 280 own German phrases cover 21 groups, not 280 separate detection algorithms.

Class gaps and physically nearest vehicles are different. Estimates are marked. A closing gap alone does not prove an opponent spun or has worn tyres. Numeric hazard distance requires fresh mapped positions and an already prepared clip; otherwise use the generic warning. Some sims do not expose opponent sectors, tyre state or weather forecasts.

Report RRCA issues in the [project issue tracker](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/issues), including build/commit, sim, track, session, exact time, expected/heard call and a sanitised log excerpt. No guaranteed support response time. Do not direct RRCA-specific bug reports to Crew Chief developers.

Credits/licences: `HINWEISE_UND_DANK.md`, `THIRD_PARTY_NOTICES.md`, `ThirdPartyLicenses`. MIT code permission does not cover voice recordings, sounds, marks or third-party templates. Resolve `FREEWARE_PRUEFUNG.md` before public promotion.

**Voice commands:** RRCA deliberately does not ship Microsoft's `Microsoft.Speech.dll`. Crew Chief's classic voice commands therefore use the Windows speech recogniser. Windows needs the matching language with speech recognition installed (Settings → Time & language → Language & region; English for the English Crew Chief commands). The AI crew (Ben, Mik, Sara) does not depend on this: it listens via its own radio button and cloud speech recognition.

Individual cockpit volume sliders for Ben/Mik/Sara: default 100%, multiplied by Messages; 0% mutes the member including Sara warnings. Saved per profile, applied to new calls and voice previews, no regenerated clips.
