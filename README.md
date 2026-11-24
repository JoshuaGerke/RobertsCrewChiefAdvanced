# RRCA – Rob's Race Crew Advanced

**The Crew Chief 4.0 extension with real AI.**

[![Downloads](https://img.shields.io/github/downloads/JoshuaGerke/RobertsCrewChiefAdvanced/total?label=Downloads&color=b9ef35)](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest)

**English** · [Deutsch](README_DE.md) · [Español](README_ES.md)

> **New in Alpha 0.38: bug fixes after the LMU race at Algarve.**
>
> - **Sara comes first.** Yellow flags, accidents, stopped and slow cars cut Ben and Mik off at once. She only cuts an answer to your question for a real hazard.
> - **Two tiers for all calls.** Important calls come at once, everything else queues and comes as soon as the radio is free. Time spent waiting behind running radio does not count as age, and a call is only dropped when its content is really out of date.
> - **Yellow with names**: "Yellow, there's an accident ahead", "Yellow, a car is stopped ahead" or "Yellow, a slow car ahead", each with the driver's name. Sara no longer reports other drivers' contacts that have no consequences.
> - **Retirements** are reported by Sara under yellow too.
> - **Sector bests in the race** are compared by Ben with the sector of the fastest lap in your class, with the name. Implausible gains from the start lap are dropped.
> - **Top speed really comes twice.** If the call is lost, it comes back later. Only cars that are still running and have a name are named.
> - **Ben's big summary**: once in races up to 30 minutes, twice up to 60 minutes, three times in longer races.
> - **The fights stay the core**: car ahead, car behind, gap to the leader and best lap comparisons come as before.
>
> **Since Alpha 0.37: best laps, top speed and clear roles.**
>
> - **Every best lap in the field is called**, with the name and the gap to your best, in practice, qualifying and the race, also on the start lap and the last lap. The call waits for a gap on the radio and only lapses once someone is faster. If you have no counted race lap yet, Ben compares with your best today or your last lap.
> - **Top speed twice per race**: in the first third and the last quarter Ben names the three fastest on the straight and your figure with your rank.
> - **Your best counts when the sim counts it.** If RaceRoom takes a lap as your best that Crew Chief marks as invalid, the call still comes.
> - **Ben answers everything general**: race situation, gaps, time left, best laps, strategy. Mik only calls fuel, consumption, tyres, technical topics and damage, short and without discussion.
> - **Fuel in practice at once**: a first estimate after a few hundred metres, then the consumption of the first full lap, even if that lap was deleted. If you ask how much fuel a race duration needs, the crew works it out in litres.
> - **Sector bests in practice and qualifying** come at once and short, with brief praise for your fastest lap.
> - **Race control for all sims**: Ben comments warnings and penalties in his own words, with names.
> - **Backmarkers** are only called when you are really catching them, not at a constant gap.
> - **Before the start** the crew no longer thinks the race is over, and it gives the time left as the current value.
>
> **Since Alpha 0.36: bug fixes.**
>
> - **Correct class in multiclass races** (LMU, AMS2, iRacing). The race summary names the winner of your class instead of the overall winner. Class position and the class top speed are right.
> - **FCY and red flag in LMU.** Under FCY or a race stoppage, half the field no longer counts as "slow" or "stopped".
> - **Fuel sources named honestly.** If you ask how much fuel a distance needs without giving a figure, the crew says where the consumption comes from: measured, projected, or estimated from practice and qualifying. Only your own figure is called "your figure".
> - **Ben does not change numbers.** A freely worded battle call can no longer turn "12 seconds" into "2 seconds".
> - **No name gets lost.** If Sara's combined call for pit stops or retirements does not get through, the names come again. With very many calls the least important one is dropped first, never a retirement.
> - **Diagnostics.** The answer time to your questions now measures only the answer itself, not another call in between.
>
> **Since Alpha 0.35: the crew calls the radio by the situation.**
>
> - **No more radio levels.** Ben, Mik and Sara decide themselves when information comes: events at once, the cars ahead and behind most often, technical reports when there is room.
> - **Spotter by radio.** "Spotter on" and "spotter off" work at once and last until restart, also when qualifying turns into the race.
> - **Nothing gets lost.** A call you never heard does not count as said and comes on the next attempt. Tyres and car condition no longer vanish with a dropped report.
> - **Sector bests in the race**, even in the middle of a battle.
> - **More honest calls.** "Closing in" only when the car behind really gets closer; "took it back" only after a real lost place. In the race a faster lap is your "best race lap", and the start lap is no longer the benchmark.
> - **Lap calls first.** "Two laps to go" and "last lap" come before the lap time.
> - **Mik takes notes on request** ("write that down").
> - **Stable.** A quick stop and restart between qualifying and the race no longer crashes Crew Chief, and the spotter mode is saved.
>
> **Since Alpha 0.32: the crew lives the race.** Ben comments on overtakes and battles with feeling and with the history of the duel, differently every time (if the AI line does not arrive within 2.5 seconds he uses the fixed sentence). Sara reports pit stops, retirements and disqualifications across the whole grid, several in one sentence, and spins and crashes behind you. In timed races Ben calls the time left at 15, 10, 5 and 2 minutes with your position. After the flag Mik also names your pit stops.
>
> **Radio commands while driving.** Press push-to-talk and say:
>
> | Say | What happens |
> |---|---|
> | **"Spotter off"** / **"Spotter on"** | Only Sara's side calls (left, right, clear). She always calls yellows, crashes, slow cars and the safety car. |
>
> This takes effect at once and holds until you restart RRCA, also from qualifying into the race and in realistic spotter mode. There is no radio amount any more: the crew always talks as the race requires. Events come at once, the cars ahead and behind most often, even more in a battle, and technical reports when there is room.

RRCA is a free, unofficial extension for [Crew Chief V4](https://gitlab.com/mr_belowski/CrewChiefV4). It gives you a pit crew of three voices that you can talk to during a race:

- **Ben** watches the race and handles strategy.
- **Mik** is your race engineer for the car, tyres and fuel.
- **Sara** is your spotter and warns you about cars next to you. She runs on your PC and never waits for the internet.

You press a button on your wheel, ask a question in plain words, and the crew answers using the live data from your sim. If the sim does not provide a value, the crew says so instead of making something up.

> **Test version.** The crew speaks **English, German or Spanish**, you choose. The app window is available in the same three languages and follows your Windows language. Calls can be wrong, late or missing. For racing simulators only.

> **Sim focus.** Right now RRCA is being tuned for **Automobilista 2**, **RaceRoom** and **Le Mans Ultimate**. **Assetto Corsa EVO** and **iRacing** come next. Other sims that Crew Chief supports work too, but are less polished so far.

**[Download the latest version](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest)** · The download also contains an illustrated step-by-step guide: `GUIDE_RRCA_EN.pdf`.

RRCA is free. If it made your race better and you have a fiver left, you can [buy us a coffee on Ko-fi](https://ko-fi.com/robertscrewchiefadvanced).

---

## Contents

1. [What you need](#1-what-you-need)
2. [What are OpenRouter and ElevenLabs?](#2-what-are-openrouter-and-elevenlabs)
3. [What does it cost?](#3-what-does-it-cost)
4. [Create your OpenRouter key](#4-create-your-openrouter-key)
5. [Create your ElevenLabs key](#5-create-your-elevenlabs-key)
6. [Install RRCA](#6-install-rrca)
7. [Set up RRCA](#7-set-up-rrca)
8. [Your first test](#8-your-first-test)
9. [Problems and answers](#9-problems-and-answers)
10. [Data and privacy](#10-data-and-privacy)
11. [Updates, uninstalling and reporting bugs](#11-updates-uninstalling-and-reporting-bugs)
12. [Technical details](#12-technical-details)
13. [Licence and thanks](#13-licence-and-thanks)

---

## 1. What you need

- A Windows PC, 64-bit (tested on Windows 11). Windows 10 and 11 normally already have the two Microsoft components RRCA needs: **.NET Framework 4.8** and the **Microsoft Edge WebView2 Runtime**. If one is missing, RRCA tells you; download it only from microsoft.com.
- A racing sim that Crew Chief supports, for example RaceRoom, Automobilista 2, Assetto Corsa Competizione, iRacing, rFactor 2 or Le Mans Ultimate.
- A **microphone** (your headset microphone is fine) and a **free button on your wheel** for talking to the crew.
- An **internet connection** while you drive.
- Two online accounts: **OpenRouter** and **ElevenLabs**. Both are explained below, step by step.

You do **not** need any programming knowledge, Python, SimHub or anything else.

## 2. What are OpenRouter and ElevenLabs?

RRCA itself is free. To understand your questions and to speak with natural voices, it uses two online services. You open an account with each, top it up with a small amount, and RRCA uses that balance. The money goes to these services, not to us.

| Service | What it does for you | Website |
|---|---|---|
| **OpenRouter** | The "brain". It turns your spoken question into text and lets Ben and Mik think up a fitting answer. OpenRouter is a marketplace that gives access to many AI models with one account and one balance. | [openrouter.ai](https://openrouter.ai) |
| **ElevenLabs** | The "voices". It turns the crew's answers into natural speech, so Ben, Mik and Sara sound like real people. | [elevenlabs.io](https://elevenlabs.io) |

Each service gives you an **API key**. That is a long password made of letters and numbers that you copy into RRCA once, so RRCA is allowed to use your account. Treat it like a password: never post it in a forum or show it in a screenshot.

## 3. What does it cost?

Prices as of **November 2026**. Prices change, so please check the providers' websites before you pay.

**OpenRouter** (prepaid, pay as you go)
- You buy credit in advance, for example 10 US dollars. OpenRouter adds a fee of 5.5 % (at least 0.80 USD) when you top up by card.
- RRCA uses a fast, low-cost model for radio answers by default and a stronger model only for longer analyses while you stand in the pits. Speech recognition costs about 0.0045 USD per minute of speech.
- Rough estimate: **a 30-minute race usually costs a few cents, rarely more than 0.25 USD.** This depends on how much you talk to the crew.
- OpenRouter may expire unused credit after one year.

**ElevenLabs** (monthly plan or pay as you go)
- **Free:** 0 USD, about 10,000 characters per month. That is enough to try things out, but not for regular racing, and not every voice can be used on the free plan.
- **Starter:** about 6 USD per month (the first month was 1 USD at the time of writing), about 75,000 characters per month. **Recommended to start with.**
- Higher plans exist if you race a lot.
- Once, during setup, RRCA prepares the crew's standard calls and Sara's spotter calls. That uses roughly 15,000 to 25,000 characters one time.
- After that, a typical race uses roughly 3,000 to 10,000 characters, depending on how much you talk to the crew and how much happens in the race.

**Tips to keep costs under control**
- Set a **spending limit** on each key (explained below).
- Start with a small top-up (for example 10 USD at OpenRouter and the Starter plan at ElevenLabs).
- In RRCA, the setting **Maximum AI calls per session** (default 300) limits how often Ben and Mik may ask the AI in one session.
- Both websites show exactly what you have used.

These numbers are estimates, not a guarantee. Your actual costs appear in your accounts.

## 4. Create your OpenRouter key

1. Go to [openrouter.ai](https://openrouter.ai) and click **Sign in** (top right). You can sign in with a Google account or with your e-mail address.
2. Open **Credits** ([openrouter.ai/settings/credits](https://openrouter.ai/settings/credits)) and click **Add Credits**. Pay a small amount, for example 10 USD, by card.
3. Open **Keys** ([openrouter.ai/settings/keys](https://openrouter.ai/settings/keys)) and click **Create Key**.
   - Name: for example `RRCA`.
   - **Credit limit:** enter an amount, for example `5`. The key then stops when this amount is used up.
4. Click **Create**. The key is shown **only once**. It starts with `sk-or-`. Copy it immediately (select it, then press Ctrl+C) and keep it somewhere safe, for example in a password manager.

## 5. Create your ElevenLabs key

1. Go to [elevenlabs.io](https://elevenlabs.io) and click **Sign up**. Confirm your e-mail address.
2. Choose a plan under **Pricing** or in your account settings. **Starter** is the safe choice (see costs above).
3. Choose voices: open the **Voice Library** (sometimes called "Voices" → "Explore"). Filter by your radio language (for example **English**) and listen to a few. Pick three you like, one each for Ben, Mik and Sara (a female voice suits Sara), and click **Add to my voices**. Only voices in "My voices" appear in RRCA later.
4. Create the key: in your account, open **API Keys** (usually under **Developers** or in your profile menu) and click **Create API key**.
   - Name: for example `RRCA`.
   - Permissions: **Text to Speech: Access** and **Voices: Read** are enough. You can leave everything else off.
   - If offered, set a **credit limit**.
5. Copy the key (it starts with `sk_`). It is usually shown **only once**.

## 6. Install RRCA

1. Download `RobertsCrewChiefAdvanced.zip` from the [download page](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest).
2. Right-click the ZIP → **Extract All…** and choose a new folder, for example `C:\Simracing\RRCA`. Do not extract it into an existing Crew Chief folder.
3. In the new folder, double-click **CrewChiefV4.exe**. If Windows shows a blue "Windows protected your PC" message, click **More info** → **Run anyway**. This appears because the program is new and not signed.
4. The RRCA window opens. The classic Crew Chief window is minimised in the taskbar behind it; both belong to the same program.

**Already using Crew Chief?** No problem. RRCA keeps its own profiles and sounds and does not change your existing Crew Chief. Do not run both at the same time.

## 7. Set up RRCA

Do all of this while the sim is closed or while you are standing in the pits, never while driving.

**a) Choose your languages.** At the top right, under **Interface language**, you can switch the window between English, Deutsch and Español. The language the crew speaks is set separately under **AI & radio** → **Radio language** (Automatic follows your Windows language). If you change the radio language later, run **Prepare radio clips** and **Generate Sara spotter pack** again (step c) and restart RRCA.

**b) Enter your keys.** Go to **Team & voices** → **Services & keys**.
- Paste the OpenRouter key into the field **OpenRouter (Ben, Mik, speech recognition)** (click into the field, press Ctrl+V) and click **Save**.
- Paste the ElevenLabs key into the field **ElevenLabs (voices)** and click **Save**.
- Click **Test connection**. RRCA shows whether the connection works.

**c) Give the crew their voices.** Still under **Team & voices**:
1. Click **Load voice list**. Your voices from "My voices" at ElevenLabs appear.
2. Choose a voice for Ben, Mik and Sara and listen with **Test**.
3. Click **Prepare radio clips**. This creates the standard calls once and takes a few minutes.
4. Click **Generate Sara spotter pack**. Wait until it is finished.
5. **Close RRCA completely and start it again.** Sara's warnings only work after this restart.

**d) Microphone and talk button.**
1. Under **Team & voices** → **Devices & push-to-talk**, choose your microphone.
2. Click **Crew Chief window** to bring the classic window to the front.
3. In the classic window, choose your wheel in the controller list, select the action **"KI-Team anfunken"** (this name is German: "call the AI team") and click **Assign**. Then press the button on your wheel you want to use. If you cannot see the action, click **Add/Remove Actions** first.
4. Go back to the RRCA window. You can minimise the classic window again.

**e) Your name.** Under **AI & radio** → **Your driver profile**, enter your name and, if you like, a short radio name (for example "Rob"). Click **Apply** at the bottom.

**f) Your sim.** On the **Cockpit** page, switch on **Detect game automatically**. RRCA then starts the crew by itself when your sim is running. Alternatively, choose the sim from the list and click **Start**. Make sure telemetry or shared memory is switched on in the sim's own settings; Crew Chief's documentation explains this for each sim.

**g) Radio reports.** There is no radio amount to set; the crew always talks as the race requires. Under **AI & radio** → **Scheduled radio reports** you only switch topics on or off. The crew sets the rhythm: in short races up to 15 minutes each technical topic comes once or twice, more often in longer races. The cars ahead and behind come most often, twice as often in a close battle. Calm phases bring tyres, damage, weather and track temperature; in a battle the technical reports wait. Events such as damage, penalties, yellows or best laps always come straight away.

## 8. Your first test

1. Start your sim and sit in the car in the pits.
2. RRCA should show your sim as connected, and the crew greets you.
3. Hold your talk button, ask something simple, for example *"How much fuel do I have?"*, and release the button.
4. Mik should answer within a few seconds.
5. Then drive a short practice session before your first race.

## 9. Problems and answers

**The crew says nothing.**
Check that the sim shows as connected in RRCA, that telemetry is enabled in the sim, and that your keys passed **Test connection**. Look at your OpenRouter and ElevenLabs balances.

**Sara does not warn me.**
Run **Generate Sara spotter pack** again, wait until it is finished, and restart RRCA. A red hint in the window explains what is missing.

**The voice list is empty.**
Add voices at ElevenLabs to **My voices**, then click **Load voice list** again. Check that the key has the permission **Voices: Read**.

**My question is not understood.**
Hold the button a moment before you speak and release it only after you have finished. Speak in your radio language. Check the selected microphone.

**It suddenly stopped talking.**
Your balance or monthly characters may be used up, or the key's credit limit was reached. Check both websites.

**Crew Chief's classic English voice commands do not work.**
These old commands use the Windows speech recogniser. Install English with speech recognition in Windows (Settings → Time & language → Language & region). Ben, Mik and Sara do not need this.

## 10. Data and privacy

- When you talk to the crew, your spoken question, the relevant race data and, if driver memory is on, matching notes are sent to OpenRouter and the AI model it uses. The text to be spoken is sent to ElevenLabs. The providers' own privacy rules apply: [OpenRouter privacy](https://openrouter.ai/privacy), [ElevenLabs terms](https://elevenlabs.io/terms-of-use-eu).
- Microphone audio is only recorded while you hold the talk button. No full recordings are stored.
- At startup RRCA asks GitHub whether a newer version exists. No personal data is sent.
- Your keys are stored encrypted for your Windows user (Windows DPAPI) and are never included in the download.
- The option **Send driver names to the AI** is off for new profiles.
- Driver memory stores notes and preferences on your PC only. It does not train any AI model.

## 11. Updates, uninstalling and reporting bugs

- **Update:** when a new version is available, RRCA shows an **Install update** button at the top. Click it and confirm: RRCA downloads and checks the update, restarts, and keeps your keys, profiles and memory. Alternatively download the new ZIP and extract it over your RRCA folder while RRCA is closed.
- **Do not** install the normal Crew Chief installer or its updates over the RRCA folder. Keep your normal Crew Chief, if you have one, in its own folder.
- **Uninstall:** close RRCA and delete the RRCA folder. Your personal data (see the table below) stays until you delete it yourself.
- **Report a bug:** open an [issue on GitHub](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/issues) or write in the [thread on virtualracing.org](https://forum.virtualracing.org/threads/rrca-erweiterung-freier-ki-boxenfunk-fuer-crew-chief-v4.146346/). Please include the version (`VERSION.txt` in the RRCA folder), sim, track, time and what you heard. **Never post your keys.** Please do not contact the Crew Chief team about RRCA.

## 12. Technical details

### Where RRCA stores data

"Documents" means your Windows Documents folder (it can be redirected to OneDrive). Each folder is only created when the related feature is used.

| What | Where |
|---|---|
| Profiles | `Documents\RRCA\CrewChiefV4\Profiles` |
| Controller settings | `…\Profiles\ControllerData` |
| Logs | `Documents\RRCA\CrewChiefV4\DebugLogs` |
| AI radio log, when enabled | `Documents\RRCA\CrewChiefV4\AiTeam\Logs` |
| Runtime data and sounds | `%LOCALAPPDATA%\RobertsCrewChiefAdvanced` and `…\Sounds` |
| Sara's spotter pack | `…\Sounds\voice\spotter_Sara` |
| RRCA window (language, cache) | `%LOCALAPPDATA%\RCCA\WebView2` |
| .NET settings | `%LOCALAPPDATA%\Britton_IT_Ltd\…\user.config` |
| Keys | `%APPDATA%\RobertsCrewChiefAdvanced\api_key.bin` and `elevenlabs_key.bin` |
| Prepared voice clips | `%APPDATA%\RobertsCrewChiefAdvanced\stimmen-cache` |
| Driver memory, when enabled | `Documents\RRCA\Fahrergedaechtnis` |
| Diagnostics recorder, when enabled | `Documents\CrewChief20\RCCA-LiveDiagnostics` |

The diagnostics recorder is limited to 128 MB per recording and 512 MB in total. It never stores keys or microphone recordings, but it can contain names and radio texts, so check it before you share it.

### Build it yourself

With Visual Studio 2022 or the Build Tools:

```
msbuild CrewChiefV4/CrewChiefV4.csproj -restore -p:Configuration=Release
./build/package.ps1 -OutDir dist/RRCA -PublicDistribution
```

## 13. Licence and thanks

Unofficial extension: RRCA is built on Crew Chief V4 by Britton IT Ltd and, like Crew Chief, is released under the [MIT licence](LICENSE). Many thanks to the Crew Chief team for this foundation. RRCA is not a product of the Crew Chief team and is not supported by them. Crew Chief's sound packs have their own rights and are not included. Third-party components: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [NOTICES_EN.md](NOTICES_EN.md).

A project by Robert and Joshua Gerke.
