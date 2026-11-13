# RRCA – Rob's Race Crew Advanced

**English** · [Deutsch](README_DE.md) · [Español](README_ES.md)

RRCA is a free, unofficial extension for [Crew Chief V4](https://gitlab.com/mr_belowski/CrewChiefV4). It gives you a pit crew of three voices that you can talk to during a race:

- **Ben** watches the race and handles strategy.
- **Mik** is your race engineer for the car, tyres and fuel.
- **Sara** is your spotter and warns you about cars next to you. She runs on your PC and never waits for the internet.

You press a button on your wheel, ask a question in plain words, and the crew answers using the live data from your sim. If the sim does not provide a value, the crew says so instead of making something up.

> **Test version.** The radio currently speaks **German** only. The app window is available in English, German and Spanish and follows your Windows language. Calls can be wrong, late or missing. For racing simulators only.

**[Download the latest version](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/tag/aktuell)** · The download also contains an illustrated step-by-step guide: `GUIDE_RRCA_EN.pdf`.

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
- After that, a typical race uses roughly 3,000 to 10,000 characters, depending on how chatty you set the radio.

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
3. Choose voices: open the **Voice Library** (sometimes called "Voices" → "Explore"). Filter by language **German** and listen to a few. Pick three you like, one each for Ben, Mik and Sara (a female voice suits Sara), and click **Add to my voices**. Only voices in "My voices" appear in RRCA later.
4. Create the key: in your account, open **API Keys** (usually under **Developers** or in your profile menu) and click **Create API key**.
   - Name: for example `RRCA`.
   - Permissions: **Text to Speech: Access** and **Voices: Read** are enough. You can leave everything else off.
   - If offered, set a **credit limit**.
5. Copy the key (it starts with `sk_`). It is usually shown **only once**.

## 6. Install RRCA

1. Download `RobertsCrewChiefAdvanced.zip` from the [download page](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/tag/aktuell).
2. Right-click the ZIP → **Extract All…** and choose a new folder, for example `C:\Simracing\RRCA`. Do not extract it into an existing Crew Chief folder.
3. In the new folder, double-click **CrewChiefV4.exe**. If Windows shows a blue "Windows protected your PC" message, click **More info** → **Run anyway**. This appears because the program is new and not signed.
4. The RRCA window opens. The classic Crew Chief window is minimised in the taskbar behind it; both belong to the same program.

**Already using Crew Chief?** No problem. RRCA keeps its own profiles and sounds and does not change your existing Crew Chief. Do not run both at the same time.

## 7. Set up RRCA

Do all of this while the sim is closed or while you are standing in the pits, never while driving.

**a) Choose your language (optional).** At the top right, under **Interface language**, you can switch between English, Deutsch and Español.

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

## 8. Your first test

1. Start your sim and sit in the car in the pits.
2. RRCA should show your sim as connected, and the crew greets you.
3. Hold your talk button, ask something simple, for example (in German) *"Wie viel Sprit habe ich?"* ("How much fuel do I have?"), and release the button.
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
Hold the button a moment before you speak and release it only after you have finished. Speak German. Check the selected microphone.

**It suddenly stopped talking.**
Your balance or monthly characters may be used up, or the key's credit limit was reached. Check both websites.

**Crew Chief's classic English voice commands do not work.**
These old commands use the Windows speech recogniser. Install English with speech recognition in Windows (Settings → Time & language → Language & region). Ben, Mik and Sara do not need this.

## 10. Data and privacy

- When you talk to the crew, your spoken question, the relevant race data and, if driver memory is on, matching notes are sent to OpenRouter and the AI model it uses. The text to be spoken is sent to ElevenLabs. The providers' own privacy rules apply: [OpenRouter privacy](https://openrouter.ai/privacy), [ElevenLabs terms](https://elevenlabs.io/terms-of-use-eu).
- Microphone audio is only recorded while you hold the talk button. No full recordings are stored.
- Your keys are stored encrypted for your Windows user (Windows DPAPI) and are never included in the download.
- The option **Send driver names to the AI** is off for new profiles.
- Driver memory stores notes and preferences on your PC only. It does not train any AI model.

## 11. Updates, uninstalling and reporting bugs

- **Update:** download the new ZIP and extract it into a new folder (or over your RRCA folder while RRCA is closed). Keys, profiles and memory are stored outside the program folder and are kept.
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
