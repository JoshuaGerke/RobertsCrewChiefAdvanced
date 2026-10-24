CrewChief version 4.16

(The Crew Chief Help menu was created from this file (and others) and has been updated since,
this file hasn't except for the change log, so it's better to refer to the Help in Crew Chief
- contents are at https://mr_belowski.gitlab.io/CrewChiefV4/index.html - than this.)

________________________________________________________________________________

Written by Jim Britton, Morten Roslev, Vytautas Leonavičius, Paul Burgess, Tony Whitley, Dan Allongo (Automobilista and rFactor1 implementation), Daniel Nowak (nAudio speech recognition port), Mike Schreiner and Brent Owen (technical input on stock car rules). The application is the result of lots of lots of hard work and input from the guys above as well as some great advice and support from the community and the guys at Sector3 and SMS.

Additional material from Scoops (fantastic track layout mapping work), Nolan Bates (conversion of thousands of phrases from speech to text for subtitle support) and Longtomjr (F1 2018 UDP data format structs). Fantastic alternate spotter sounds by Geoffrey Lessel, Matt Orr (aka EmptyBox), Clare Britton, Mike Schreiner, Phil Linden, Lee Taylor and Micha (last name?). Also a thank you to Nick Thissen for his work on iRacingSdkWrapper.  Rally mode was created in collaboration with Janne Laahanen, who contributed his codriver pack and helped in understanding of the RBR pacenotes.

The source code for Crew Chief is available at https://gitlab.com/mr_belowski/CrewChiefV4

For support and discussions about Crew Chief we have our very own forum here: https://thecrewchief.org/

The full changelog is now at the end of this file.


Quick start
-----------
You need to install .net 4.7.2 or above to use the app. Download the CrewChiefV4.msi installer and run it. Start the app. Click the "Download sound pack" button and the "Download driver names" button to get the latest sounds and driver names. Select a game from the list at the top right. When the sounds and driver names have finished downloading, click the "Start Application" button. Then fire up the game. Note that the app comes with swearing 'off' by default - if you want to be sworn at you need to enable this in the Properties UI.


Running with voice recognition
------------------------------
There are two voice recognition systems which Crew Chief can use. The first of these is optimised for noisy environments and poor quality sound input. This is the default option. It requires no training and works with a wide range of microphone types and accents, but it requires a separate runtime installation and a language pack. This system is referred to as the "Microsoft speech recognition engine". The other system uses the speech recognition system built in to Windows. This requires a better quality voice input signal than the default system but can be trained to recognise a user's individual voice. It does not require the installation of any additional components. This system is referred to as the "Windows speech recognition engine".

Which is best depends on the quality of your microphone, your voice, the amount of background noise, and countless other factors. It's worth trying both and seeing which gives the best results.

If you want to use the Windows speech recognition engine simply ensure that the 'Prefer Windows speech recogniser' option is checked on the Properties screen. You will get better results if you work through the speech recognition training process in Windows.

If you want to use the default Microsoft voice recognition system, download the correct speech recognition installers for your system (speech_recognition_32bit.zip or speech_recognition_64bit.zip). Run SpeechPlatformRuntime.msi (this is the MS speech recognition engine), then run MSSpeech_SR_en-GB_TELE.msi or MSSpeech_SR_en-US_TELE.msi depending on your preferred accent (these are the 'cultural info' installers). If you want to use US speech recognition (MSSpeech_SR_en-US_TELE.msi) you must modify the "speech_recognition_location" property to "en-US". This can be done by editing CrewChiefV4.exe.config, or by modifying the property value in the application's Properties area. If you're happy with en-GB you don't need to do anything other than run the 2 speech recognition installers.

Note that the app will fall back to using the Windows speech recognition engine if it can't find a working installation of the Microsoft speech recognition engine.

For both speech recognition systems, you need a microphone configured as the default "Recording" device in Windows.

To get started, run CrewChiefV4.exe and choose a "Voice recognition mode". There are 4 modes (the radio buttons at the bottom right). "Disabled" means that the app won't attempt any speech recognition. "Hold button" means you have to hold down a button while you speak, and release the button when you're finished. "Toggle button" means you press and release a button to start the speech recognition, and the app will listen until it hears a voice command. "Always on" means the app is always listening for and processing speech commands. "Trigger word" means the app is always listening for a particular word or phrase (default "Chief", can be changed). When it hears this trigger it'll start listening for a regular voice command - a bit like the approach taken with Alexa / OK Google. Selecting "Disabled", "Always on" or "Trigger word" from this list makes the app ignore the button assigned to "Talk to crew chief".

If you want to use Hold button or Toggle button mode, select a controller device ("Available controllers" list, bottom left), choose "Talk to crew chief" in the "Available actions" list and click "Assign control". Then press the button you want to assign to your radio button. 

You need to speak clearly and your mic needs to be properly set up - you might need to experiment with levels and gain (Microphone boost) in the Windows control panel. If he understood he'll respond - perhaps with helpful info, perhaps with "we don't have that data". If he doesn't quite understand he'll ask you to repeat yourself. If he can't even tell if you've said something he'll remain silent. There's some debug logging in the main window that might be useful.

I've not finished implementing this but currently the app understands and responds to the following commands:

"how's my [fuel / tyre wear / body work / aero / engine / transmission / suspension / pace ]"
"how are my [tyre temps / tyre temperatures / brakes / brake temps / brake temperatures / engine temps / engine temperatures]" (gives a good / bad type response)
"What are my [brake temps / tyre temps]" (gives the actual temps)
"what's my [gap in front / gap ahead / gap behind / last lap / last lap time / lap time / position / fuel level / best lap / best lap time]"
"what's the fastest lap" (reports the fastest lap in the session for the player's car class)
"keep quiet / I know what I'm doing / leave me alone" (switches off messages)
"keep me informed / keep me posted / keep me updated" (switches messages back on)
"how long's left / how many laps are left / how many laps to go"
"spot / don't spot" (switches the spotter on and off - note even in "leave me alone" mode the spotter still operates unless you explicitly switch it off)
"do I still have a penalty / do I have a penalty / have I served my penalty"
"do I have to pit / do I need to pit / do I have a mandatory pit stop / do I have a mandatory stop / do I have to make a pit stop"
"where's [opponent driver last name]"
"what's [opponent driver last name]'s last lap"
"what's [opponent driver last name]'s best lap"
"what's [opponent race position]'s last lap" (for example, "what's p 4's best lap", or "what's position 4's last lap")
"what's [opponent race position]'s best lap"
"what's [the car in front / the guy in front / the car ahead / the guy ahead]'s last lap"
"what's [the car in front / the guy in front / the car ahead / the guy ahead]'s best lap"
"what's [the car behind / the guy behind]'s last lap"
"what's [the car behind / the guy behind]'s best lap"
"what tyre(s) is [opponent driver last name / opponent race position] on"
"what are my sector times"
"what's my last sector time"
"what's the [air / track] [temp / temperature]
"who's leading" (this one only works if you have the driver name recording for the lead car)
"who's [ahead / ahead in the race / in front / in front in the race / behind / behind in the race]" (gives the name of the car in front / behind in the race or on the timing sheet for qual / practice. This one only works if you have the driver name recording for that driver)
"who's [ahead on track / in front on track / behind on track]" (gives the name of the car in front / behind in on track, regardless of his race / qual position. This one only works if you have the driver name recording for that driver)
"tell me the gaps / give me the gaps / tell me the deltas / give me the deltas" (switch on 'deltas' mode where the time deltas in front and behind get read out on each lap. Note that these messages will play even if you have disabled messages)
"don't tell me the gaps / don't tell me the deltas / no more gaps / no more deltas" (switch off deltas mode)
"repeat last message / say again" (replays the last message)
"What are my [brake / tyre] [temperatures / temps]"
"What time is it / what's the time" (reports current real-world time)
"What's my fuel usage / what's my fuel consumption / what's my fuel use" (reports the per-lap or per-minute average fuel consumption)
"What tires am I on / what tire am / on / what tire type am i on" (reports the tyre name you're currently using, if available)
"Calculate fuel for [X minutes / laps] / how much fuel do I need for [X minutes / laps] / how much fuel for [X minutes / laps]" (estimates how much fuel you'll probably need for this many minutes or laps)
"Give me tyre pace differences / what are the tire speeds / whats the difference between tires / compare tire compounds" (Raceroom only - gives lap time deltas for the best lap on each tyre type that's been used during the session, across all drivers in the same car class as the player)
"This is the formation lap / formation lap / rolling start": Enable manual rolling-start mode (used by some online leagues)
"Standing start / no formation lap": Disable manual rolling-start mode
"Where should I attack / where am I faster / where can I attack": If the app has enough data, will report the corner name where you're gaining the most time on the guy in front
"Where should I defend / where am I slower / where is he faster / where will he attack": If the app has enough data, will report the corner name where you're losing the most time to the guy behind
"Read corner names / corner names / tell me the corner names": read out each corner name when you hit the mid-point of the corner, for this lap only (useful to test corner name mappings)
"Damage report" / "How's my car" / "Is my car ok?": report any damage the car has sustained
"Car status": report any damage the car has sustained, tyre and brake temperature status and fuel / battery status
"Session status" / "Race status": report race position, gaps, time / laps left in session
"Full update" / "Full status" / "Update me": combines all of the above three status reports (will produce a very verbose response)
"how much fuel to the end" / "how much fuel do we need": report how many litres or gallons of fuel the app thinks you'll need to finish the race
"is the car ahead in my class" / "is the car ahead my class" / "is the car ahead the same class as me" / "is the car in front in my class" / "is the car in front my class" / "is the car in front the same class as me" - responds yes or no
"is the car behind in my class" / "is the car behind my class" / "is the car behind the same class as me" - responds yes or no
"what class is the car ahead" / "what class is the guy ahead" / "what class is the car in front" / "what class is the guy in front" - reports class name, or "faster" / "slower" if the app doesn't have the class name
"what class is the car behind" / "what class is the guy behind"- reports class name, or "faster" / "slower" if the app doesn't have the class name
"time this stop" / "practice pitstop" / "time this pitstop" / "pitstop benchmark": time the next pitstop (sector 3 + sector 1 time) to work out home much tome overall a pitstop costs - practice session only
"where will I be after a stop?" / "Estimate pit exit positions" / "What will happen if I pit?": play estimate of traffic and race positions on pit exit, if you were to pit on this lap
"don't talk in the [corners / braking zones]" / "no messages in the [corners / braking zones]": delay non-critical messages if the player is in a challenging section of the track
"talk to me anywhere" / "messages at any point": disable message delay in challenging parts of the track
"set alarm to [hour] [minutes] [optional am/pm]" / "alarm me at [hour] [minutes] [optional am/pm]": sets the alarm clock at the given time, supports both 12 and 24 hour format
"clear alarm clock" / "clear alarms": clears all the alarms set
"enable cut track warnings" / "play cut track warnings:warn about cuts"
"no cut track warnings" / "no more cut warnings:no more cut track warnings"
"watch [opponent driver last name]" (adds this driver to a list of drivers which the app will keep you informed about)
"team mate [opponent driver last name]" (adds this driver to a list of drivers which the app will keep you informed about)
"rival [opponent driver last name]" (adds this driver to a list of drivers which the app will keep you informed about)
"cancel watched drivers" / "stop watching drivers" / "stop watching all" (clears all watched drivers)

iRacing-specific pit commands:

"pitstop add [X liters]" (adds X amount of fuel next pitstop, this option is iRacing only)
"pitstop tearoff / pitstop windscreen" (enable next pitstop, this option is iRacing only)
"pitstop fast repair / pitstop repair" (enable fast repair next pitstop, this option is iRacing only)
"pitstop clear all" (clears all selected pitstop options, this option is iRacing only)
"pitstop clear tyres" / "pitstop don't change tyres" / "box, clear tyres" / "box, don't change tyres" (clears all tyre selections next pitstop, this option is iRacing)
"pitstop clear tearoff / pitstop clear windscreen" (clears tearoff selection next pitstop, this option is iRacing only)
"pitstop clear fast repair" (clears fast repair selection next pitstop, this option is iRacing only)
"pitstop clear fuel" (clears fuel refueling next pitstop, this option is iRacing only)
"pitstop change all tyres" / "box, change all tyres" (change all tyres next pitstop, this option is iRacing)
"pitstop change left front tyre" (change left front tyre next pitstop, this option is iRacing only)
"pitstop change right front tyre" (change right front tyre next pitstop, this option is iRacing only)
"pitstop change left rear tyre" (change left rear tyre next pitstop, this option is iRacing only)
"pitstop change right rear tyre" (change right rear tyre next pitstop, this option is iRacing only)
"pitstop change left side tyres"(change left side tyres next pitstop, this option is iRacing only)
"pitstop change right side tyres"(change right side tyres next pitstop, this option is iRacing only)
"pitstop change tyres pressure [ new value ]" (change right rear tyre pressure next pitstop, this option is iRacing only)
"pitstop change left front tyre pressure [ new value ]" (change left front tyre next pressure pitstop, this option is iRacing only)
"pitstop change right front tyre pressure [ new value ]" (change right front tyre pressure next pitstop, this option is iRacing only)
"pitstop change left rear tyre pressure [ new value ]" (change left rear tyre pressure next pitstop, this option is iRacing only)
"pitstop change right rear tyre pressure [ new value ]" (change right rear tyre pressure next pitstop, this option is iRacing only)
"pitstop fuel to the end" / "pitstop fuel to the end of the race" (add the fuel amount the app calculates you'll need to finish the race, this option is iRacing only)


R3E-specific commands:

"pitstop clear tyres" / "pitstop don't change tyres" / "box, clear tyres" / "box, don't change tyres"
"pitstop change all tyres" / "box, change all tyres"
"pitstop change front tyres only" / "box, change front tyres only"
"pitstop change rear tyres only" / "box, change rear tyres only"
"pitstop next tyre compound" / "box, next tyre compound"
"pitstop fix front aero only" / "box, fix front aero only"
"pitstop fix rear aero only" / "box, fix rear aero only"
"pitstop fix all aero" / "box, fix all aero"
"pitstop don't fix aero" / "box, don't fix aero"
"pitstop fix suspension" / "box, fix suspension"
"pitstop don't fix suspension" / "box, don't fix suspension"
"pitstop serve penalty" / "box, serve penalty"
"pitstop don't serve penalty" / "box, don't serve penalty"
"pitstop refuel" / "box, refuel"
"pitstop don't refuel" / "box, don't refuel"
"what are the pit actions" / "what's the pitstop plan" (reports the selected actions for the next pitstop)
"what's [opponent driver last name]'s ranking" (reports the R3E ranking for this driver if available)
"what's [opponent driver last name]'s reputation" (reports the R3E reputation for this driver if available) 
"what's [opponent driver last name]'s rating" (reports the R3E rating for this driver if available)
"what's [opponent race position]'s ranking" (reports the R3E ranking for this driver if available)
"what's [opponent race position]'s reputation" (reports the R3E reputation for this driver if available) 
"what's [opponent race position]'s rating" (reports the R3E rating for this driver if available)
"what's [the car in front / the guy in front / the car ahead / the guy ahead]'s ranking" (reports the R3E ranking for this driver if available)
"what's [the car in front / the guy in front / the car ahead / the guy ahead]'s reputation" (reports the R3E reputation for this driver if available) 
"what's [the car in front / the guy in front / the car ahead / the guy ahead]'s rating" (reports the R3E rating for this driver if available)
"what's [the car behind / the guy behind]'s ranking" (reports the R3E ranking for this driver if available)
"what's [the car behind / the guy behind]'s reputation" (reports the R3E reputation for this driver if available) 
"what's [the car behind / the guy behind]'s rating" (reports the R3E rating for this driver if available)


Speech recognition customisation
--------------------------------
If you want to change the phrases the app listens for (e.g. instead of asking "how's my tyre wear", perhaps you want to as "how's my boots looking"), create a file called "speech_recognition_config.txt" in [user]\AppData\Local\CrewChiefV4 and use this to override the defaults found in [installDir]\speech_recognition_config.txt


Free dictation chat messages (experimental, iRacing, pCars2 and R3E only)
-------------------------------------------------------------------------
The app can attempt to recognise a chat message if it's using the Windows speech recognition engine (the one built into Windows that requires training). This feature can be enabled with the 'Enable free dictation chat messages' property. The app will expect the message to start with the 'Chat free dictation start word' property value (default is "chat"). If this feature is enabled and you make a voice command that starts with this word, the app will attempt to recognise all the speech input after this word. It will execute the "start chat macro" macro (presses the chat key - 't' or 'c' depending on the game), then it'll type the characters of the recognised speech, then execute the "end chat macro" macro (presses the 'enter' key).

For example, if you make a voice command "chat, good luck everyone" the app will press the key to activate the in-game chat, type 'good luck everyone' and press end. If it works. Note that this is heavily dependent on the accuracy of the free dictation speech recogniser and is just as likely to type 'good book ever known' or other such nonsense. I *strongly* recommend going through the speech recognition training process fully before using this feature. It's also a good idea to test it with Notepad or another text editor running in the foreground first so you can see what it would actually be typing when you make a command. 


Other button assignments
------------------------
You can assign the 'toggle spotter on/off', 'toggle race updates on/off', 'toggle opponent deltas' and 'repeat last message' to separate buttons if you want to be able to toggle the spotter function and toggle the crew chief's updates on or off during the race. This doesn't require voice recognition to be installed - simply run the app, assign a button to one or both of these functions, and when in-race pressing that button will toggle the spotter / crew chief / opponent deltas on and off.


Properties
----------
When you first run the app it will create a user configuration folder in /Users/[username]/AppData/local/CrewChiefV4 (for example, on my system this is in C:\Users\Jim\AppData\Local\CrewChiefV4). This folder holds your application settings. The settings can be accessed by clicking the "Properties" button in the app. This displays a popup window where you can tweak stuff if you want to. This interface is a bit rubbish but should let you tweak settings if you want to, although the properties are all (currently) undocumented. If you do change something in this interface, the app needs to restart to pick up the change - the "Save and restart" button should do this.

Each property has a "reset to default" button, or if you get completely stuck you can close the app and delete the user configuration folder and it should reset everything.


Custom controllers
------------------
This is untested. If your controller doesn't show up in the list of available controllers you can set the "custom_controller_guid" property to the GUID of your controller device. If this is a valid controller GUID the app will attempt to initialise it an add it to the list of available controllers.


Program start arguments
-----------------------
If you want to have the game pre-selected, start the app like this for PCars: [full path]\CrewChiefV4.exe PCARS_64BIT. Or use R3E or PCARS_32BIT.
This can be used in conjunction with the launch_pcars / launch_raceroom / [game]_launch_exe / [game]_launch_params and run_immediately options to set crew chief up to start the game selected in the app launch argument, and start its own process. I'll provide examples of this approach soon. 


rFactor2 Unofficial Features
-------------------------------------
Crew Chief supports some rF2 specific features not exposed via official rF2 Internals API.  Those features are turned off by default.  To enable those features, modify UserData\player\CustomPluginVariables.json by setting "EnableDirectMemoryAccess" to "1".  Plugin configuration should look like this:
  "rFactor2SharedMemoryMapPlugin64.dll":{
    " Enabled":1,
    "DebugISIInternals":0,
    "DebugOutputLevel":0,
    "DedicatedServerMapGlobally":0,
    "EnableDirectMemoryAccess":1
  }

Note: first space in " Enabled" above is required.

See this thread for more information: https://thecrewchief.org/showthread.php?1011-rFactor-2-Unofficial-Features


Pit exit position prediction
----------------------------
Version 4.9.3.0 adds rudamentary race and track position predictions for pit exit. This is based on a benchmark pitstop time which uses the time difference between your best lap's sector3 and sector1 times, and the corresponding times on your inlap & outlap.

Benchmark pit times can be measured during a practice session with the 'time this stop' / 'practice pitstop' / 'time this pitstop' / 'pitstop benchmark' voice command, or by pressing the "Pitstop prediction" button before make your practice stop. Do some hot laps to get a baseline laptime, then issue this after your 'box this lap' command, and do a full race-simulation pitstop. Maintain race pace on your outlap until you finish sector1. The app will calculate how much time you lost and use this when you pit in the race. If you don't measure your own benchmark pit time the app will measure the time your opponents lose due to pitting and use this instead - note that this means pit exit prediction data won't be available until after at least one of your opponents have completed a pitstop.

If the app has usable benchmark data and you request a pitstop *before you reach sector3*, it'll tell you where you should come out and what the traffic will be like when you hit sector3. You can also request this data at any time with the 'Where will be be after a stop?' / 'Estimate pit exit positions' / 'What will happen if I pit?', or by pressing the "Pitstop prediction" button during a race, even if you've not requested a pitstop (the app will derive its estimates on every lap regardless or whether you actually pit).

At the time of writing there's more work to be done here and some more features that may be added.


R3E Pit Menu Interactions
-------------------------
R3E exposes some additional information describing the state of the popup pit menu. Using these, the app is able to navigate the menu by pressing sequences of key to make specific pit requests. This depends on the in-game key bindings matching the keys set up in the app's command macros. By default, these macros require the in-game pit menu actions to be bound to w (menu up), a (menu left / decrease), s (menu down), d (menu right / increase), q (menu toggle), e (menu select) and r (request pit). It uses the menu navigation command macros (single button presses to move the cursor) to locate and select the approprate commands for most actions like selecting / deselecting tyres and repairs. Choosing a refuelling amount is not possible with this new approach - it still relies on the auto fuelling macros. The commands which use this new approach are:

"pitstop clear tyres" / "pitstop don't change tyres" / "box, clear tyres" / "box, don't change tyres"
"pitstop change all tyres" / "box, change all tyres"
"pitstop change front tyres only" / "box, change front tyres only"
"pitstop change rear tyres only" / "box, change rear tyres only"
"pitstop fix front aero only" / "box, fix front aero only"
"pitstop fix rear aero only" / "box, fix rear aero only"
"pitstop fix all aero" / "box, fix all aero"
"pitstop don't fix aero" / "box, don't fix aero"
"pitstop fix suspension" / "box, fix suspension"
"pitstop don't fix suspension" / "box, don't fix suspension"
"pitstop serve penalty" / "box, serve penalty"
"pitstop don't serve penalty" / "box, don't serve penalty"
"pitstop refuel" / "box, refuel"
"pitstop don't refuel" / "box, don't refuel"
"pitstop next tyre compound" / "box, next tyre compound"
"pitstop hard tyres" / "box, hard tyres"
"pitstop medium tyres" / "box, medium tyres"
"pitstop soft tyres" / "box, soft tyres"
"pitstop prime tyres" / "box, prime tyres" / "pitstop primary tyres" / "box, primary tyres"
"pitstop option tyres:" / "box, option tyres"
"pitstop alternate tyres" / "box, alternate tyres"
"what are the pit actions" / "what's the pitstop plan" (reports the selected actions for the next pitstop)

The app will also read out the planned pit actions automatially when you get near the pit entrance after requesting a stop.



Overlays
--------
Crew Chief can render in-game overlays to show the app's console output or user-configurable telemetry charts controlled by voice commands. This only works with the game running in windowed (or borderless windowed) mode.

These can also be shown in VR and positioned in the game world using 3rd party apps like "OVR Overlay". Enable this functionality with the 'Enable overlay window' property. By default the app will record the telemetry data specified in Documents/CrewChiefV4/chart_subscriptions.json - this file can be edited to include new data channels but it requires some understanding of the internal game data format and / or the internal Crew Chief data format - we're still working in this. The telemetry recording is not active during race sessions - you can enable this by selecting with the 'Enable chart telemetry in race session' property.

Telemetry is available for the previous completed lap (excluding out laps), the player's best lap in the session (excluding invalid or incomplete laps) and in some limited cases, the best opponent lap in the session (opponent data is limited to car speed, and is only available for some games at the moment). Typically, you'd drive a few laps in practice and pit, then ask the Chief "show me last lap car speed" or something like that.

To get the maximum out of telemetry charts in iRacing, iRacing disk telemetry must be enabled in iRacing app.ini 'irsdkEnableDisk', and in Crew Chief properties 'Enable disk based telemetry for overlay'. Crew Chief can not access the disk based telemetry while its recording but once your get out of the car or stop the recording manually (Alt-L) Crew Chief will read and process the file and you can get access to the data on the overlay.

The charts can be zoomed in to show a particular sector, or zoomed and panned with voice commands.

General overlay commands:

"hide overlay" / "close overlay"
"show overlay"
"show console"
"hide console"
"show chart"
"hide chart"
"show all overlays" - show console and chart(s)
"new chart" / "clear chart" - removes all data from the currently rendered chart
"clear data" - clears all in-memory telemetry data for the current session
"refresh chart" - redraw the current chart with the latest data
"show stacked charts" - show each different series on its own chart
"show single chart" - show all active series on the same chart
"show time" - change the x-axis to be time
"show distance" - change the x-axis to be distance around the lap (this is the default)
"Show sector [one / two / three]"
"Show all sectors"
"Zoom [in / out]"
"Pan [left / right]"
"Reset zoom" - reset zoom to show the entire lap's data
"Next lap" - show the next lap (when showing 'last lap' chart)
"Previous lap" - show the previous lap (when showing 'last lap' chart)
"Show last lap" - move back to the last lap (when showing 'last lap' chart)


Series specific overlay commands:

"show me..." - add a series to the chart
"chart, remove..." - remove a series from the chart
"best lap..." - add player best lap to the chart
"last lap..." - add player last lap to the chart (can be omitted - the app will assume you mean last lap if you don't specify it)
"opponent best lap.." - add opponent best lap (over all opponents in the player's class) to the chart

e.g. to show a single chart for car speed, with your best lap and your opponents's overall best lap overlaid on the same chart with x-axis as distance (metres):
"show me best lap car speed"
"show me opponent best lap car speed"

e.g. to show 2 charts, one for speed and one for gear for your best and your last lap with x-axis as distance (metres):
"show me best lap car speed"
"show me last lap car speed"
"show me best lap gear"
"show me last lap gear"

e.g. to show 3 charts, speed gear and RPM for your best and last laps with x-axis as time (seconds):
"show me best lap car speed"
"show me last lap car speed"
"show me best lap gear"
"show me last lap gear"
"show me best lap engine revs"
"show me last lap engine revs"
"show time"

e.g. to show a single chart with throttle position and gear for your last lap with x-axis as distance (metres):
"show me throttle position"
"show me gear"
"show single chart"


Note that data for the same series (e.g. car speed) will always be overlaid on the same chart. Stacked charts only applies to data from different series (speed / gear, for example).

The definition of a "series" is held in Documents/CrewChiefV4/chart_subscriptions.json. You can add to this as you wish but bear in mind that opponent data is very limited - for some games no suitable data is available at all, for some only car speed is available. We'll add more here and documentation as we go along. The rawDataFieldName refers to a field in the raw game data (the shared memory block), use this to access unmapped data or use "mappedDataFieldName" to access data that CrewChief has mapped from the raw data. In both cases dot-notation is supported. For example, car speed can be obtained from R3E by using rawDataFieldName=CarSpeed, or from the mapped data (for all games) using mappedDataFieldName=PositionAndMotionData.CarSpeed. All opponent data comes from mapped data, and the only available fields are Speed, RPM and Gear.

The voice command fragment for each series is also in this json file. The voice command is constructed by the app, prefixing "show me best lap..." / "show me last lap..." / "show me opponent best lap..." as appropriate (e.g. "show me last lap car speed").

The overlay can also be controlled with the mouse. Enable the "Enable input" checkbox on the overlay to show controls for the various chart and overlay functions. The overlay can also be moved around by dragging the title bar when Enable input is checked.

For Occulus users the overlay can be rendered as a separate application window, allowing it to be added to the VR world - enable the 'Enable overlay app window (Oculus mode)' checkbox in the Properties screen.


Known Issues Which Aren't Fixable
---------------------------------

Project Cars doesn't send opponent laptime data, so the app has to time their laps. In practice and qual sessions this is fairly reliable (because the app can use the time remaining in the session, sent by the game, for its 'clock' when timing). In race sessions with a fixed number of laps the app has nothing it can use as a clock to time the laps, so times them itself. This can lead to opponent lap / sector time inaccuracies if the player pauses the game (the app's clock is still running).


Joining a session part way through (practice or qualify session online) will result in the app having an incomplete set of data for opponent lap and sector times. In such cases the best opponent lap and sector data is inaccurate. For Project Cars, there's nothing I can do about this. The opponent lap and sector times aren't in the shared memory (the app has to time their laps), so the pace and sector delta reports may be inaccurate (they use the fastest lap completed while the app is running). For Raceroom we can get the fastest opponent lap time, but if this lap was completed before the app was running, the sectors within that lap aren't accessible. In this case the pace report will include the lap time delta, but there'll be no sector delta reports.

In both cases as soon as an opponent sets a faster lap, the app will have up to date best lap data so the pace and sector reports will be accurate and complete.


Project Cars doesn't send opponent car class data, so the app has to assume that all drivers in the race are in the same car class. For multiclass races, all pace and other reports will be relative to the overall leader / fastest car.


RaceRoom uses a 'slot_id' field to uniquely identify drivers in a race. However, this field doesn't really work properly (there are lots of issues with it), so the app has to use the driver's names. Driver names for AI driver are not unique. All the lap time and other data held for each driver is indexed by driver name so if a race has 2 or more drivers with the same name, the app will get things like lap and sector times wrong. This is only a problem racing the AI - be aware that if you have a car class with a limited number of unique AI drivers (Daytona Prototypes / German Nationals / Americal Nationals / Hill Climb Legends / etc), but select a field size greater than this, the app will do weird things.


RaceRoom doesn't have a pre-start procedure phase for offline races, and in the pre-start phase online ("Gridwalk") very little valid and accurate data is available.


Project Cars doesn't have a distinct pre-start procedure phase. I've added some more messages before the 'get ready' but there's a risk here that they might delay the 'get ready' message.


Detecting 'good' passes isn't really feasible. I've tried to limit the 'good pass' messages to overtakes that are reasonably 'secure', don't result in the other car slowing excessively, and don't involve the player going off-track. I can't, for example, tell the difference between a clean pass and a bump-and-run punt, so you might get congratulated for driving like a berk.


Updating the app
----------------
If a new version of the app is available the auto updater will prompt you to download it. This will download and run a new .msi installer - just point it at the existing install location and it'll update your old installation. It won't remove your existing sound pack or your settings.

If a new sound pack or driver names pack is available the appropriate Download button(s) will be enabled - these will download and unpack the updated sounds / driver names, then restart the application.

the 64bit speech recognition installers can be downloaded here 	 : https://drive.google.com/file/d/0B4KQS820QNFbY05tVnhiNVFnYkU/view?usp=sharing
the 32bit speech recognition installers can be downloaded here   : https://drive.google.com/file/d/0B4KQS820QNFbRVJrVjU4X1NxSEU/view?usp=sharing


Crew Chief SDK/Shared Memory
----------------------------
Crew Chief now exposes some of its internals via Shared Memory block. Currently, subtitle information is exposed, but we might be adding more data in the future. See https://gitlab.com/mr_belowski/CrewChiefV4/-/blob/master/CrewChiefV4SDK/CrewChiefV4SDK/Program.cs to get started.


Crew Chief Command line parameters
----------------------------------
-game [GAME_NAME] - Specify game to select in the Crew Chief

Example: [full path]\CrewChiefV4.exe -game RACE_ROOM
this will make RaceRoom the selected game in the Crew Chief.

Supported values:
RACE_ROOM, PCARS2, PCARS_64BIT, PCARS_32BIT, PCARS_NETWORK, PCARS2_NETWORK, RF1, ASSETTO_64BIT, ASSETTO_32BIT, RF2, RF2_64BIT, IRACING, F1_2018, F1_2019, ACC, AMS2, AMS2_NETWORK, AMS, FTRUCK, MARCAS, GSC


-profile [file name]- You can specify name of the profile to run at CC startup

Example: [full path]\CrewChiefV4.exe -profile "my favorite game my awesome profile"
this will load "my favorite game my awesome profile.json" profile at Crew Chief startup.


-cpu[1-8] - You can set the processor affinity for Crew Chief in TaskManager, but this will have to be done each time you start the app. Alternatively, you can start the app with an addition argument "-cpu1", "-cpu2", ... "-cpu8", like this:

Example: [full path]\CrewChiefV4.exe -cpu4
this will set the processor affinity to the 4th CPU in your system (usually referred to as CPU3 - they're zero-indexed).


-c_exit - Pass this switch to close running Crew Chief instance.


-nodevicescan - Disable automatic active/disabled controller detection.  Use this if you have issues with CC rescanning controllers all the time (caused by buggy device drivers).


-sound_test - Enables extra UI that helps sound pack creators testing sounds.


-skip_updates - disables the check for CC updates.


-debug - collects CC debug trace.  For more info see here: https://thecrewchief.org/showthread.php?142-How-to-collect-Crew-Chief-repro-traces



Donations
---------
We built and maintain this because we want to, we enjoy making stuff, and contributing to the Sim Racing community is awesome. Working with the various quirks, errors and omissions in the shared data which the games provide isn't much fun but it's all part of the challenge. Having said that, there are many many hours of hard work invested in this.
If you use it and like it and it becomes a regular and positive part of your sim racing, we'd be grateful if you would consider making a small donation. If only to stop our wives from complaining at us.

The Crew Chief paypal address is jim.britton@yahoo.co.uk

Or you can use this to donate directly:

https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=LW33XFXP4DPZE

Would be great to recoup some of the investment in making this, but the most important thing is that the app is used 'in anger' and enjoyed as part of the sim racing experience. To this end, we're always on the lookout for bug reports and feature suggestions.

One final point. If the app says "Jim is faster than you", let him through :)


Changelog
---------
Version 4.17.1.4: Fix some issues with fuel reporting; Some MQTT telemetry improvements; AMS2 - wire up tyre pressures; R3E - use game-provided car dimensions for spotter; GTR2 - plugin update

Version 4.17.1.2: Fixed TTS (text-to-speech) for driver names; fix some speech recognition issues and reinstated the short position phrases disabled in the last release

Version 4.17.1.1: Correct some bugs in the driver name mapping which was preventing some valid driver names from being read; Speech recognition bug fixes for driver names - note this includes removing the short position phrases ("who's in P1", "where's P12" etc) - use the longer versions ("who's in position 1", "where's position 12" etc) instead; GTR2 - plugin update

Version 4.17.1.0: Rework opponent name matching to be more accurate and more conservative; added UI to modify suppress or manually override guessed opponent name matches ("Opponent Names" from the app's menu bar)

Version 4.17.0.0: Use FuzzySharp to find names close to the player's name - the My Name popup now allows you to enter your name and the app will suggest possible matches for this. Sets Allow opponent names in 'My name' list Property if necessary; allow fuzzy and phonic matching for opponent driver names - enabled by default, can be toggled off with the 'Fuzzy opponent name matching' property; MQTT parses JSON messages with priority and distance at which the message should be played; Plugin installer now sets folder browser root to My Computer instead of Desktop; RF2 - Update rFactor2SharedMemoryMapPlugin64.dll to 3.7.15.1; ACC - Added car model to data; R3E - use game provided brake temperature thresholds; AMS2 - Use US terms for AMS2's indycars, some car class corrections; iRacing - Tsukuba track mapped, LMDH added, Formula Ford 1600 added; GTR2 - Updated plugin; various minor fixes

Version 4.16.3.5: R3E - fix missing ADAC 2021 GT3 class mapping; GTR2 plugin update; added more channels to MQTT telemetry output; a few minor fixes

Version 4.16.3.4: GTR2 plugin update; Added basic support for AMS2 full course yellow flags; Various minor fixes

Version 4.16.3.2: GTR2 plugin update; Allow MQTT broker responses to trigger arbitrary text-to-speech responses; some minor fixes

Version 4.16.3.1: Fix missing 'conditions' data (weather, track temp etc); Added option to report fuel remaining in number of laps, even for time-limited races ('Report fuel as laps left in timed races' in the Properties screen); RF2 - added 'display next' voice command to move to the next MFD screen; ACC - corrected car speed data (use a more frequently updated value); Various minor fixes

Version 4.16.3.0: Support change of hosting location for assets; New configuration setting to publish live telemetry data to an MQTT broker. A default broker is hosted at <a href="https://github.com/b4mad/racing">github.com/b4mad/racing</a>. More under Help &rarr; Telemetry; Iracing - fix some issues with battery charge reporting; RF2 - clear existing tyre change setting when starting a race; General bug fixes

Version 4.16.2.6: Added 'listen if not pressed' as a speech recognition mode - the app will always listen for voice commands unless the assigned button is held down (useful for cases where that button activates some other program's voice input); iRacing - fix the app not working with electric vehicles. Note that there's more work still to do here, the existing CC support for electric vehicles makes some assumptions about charging / vehicle swap rules that may not apply

Version 4.16.2.5: Revised audio device re-indexing to better handle cases where audio devices are changed while the app is running; fix some crash bugs on start up; assorted other minor fixes and tweaks; GTR2 - plugin update; iRacing - speculative workaround to prevent the app trying to auto-fuel on driver change when it shouldn't; AMS2 - marked some tracks of Ovals

Version 4.16.2.3: Added an option to insert a pause between sounds ('Pause between sound fragments (ms)' in the Properties screen, units milliseconds). If you find that the app sometimes misses off the start or end of some sounds, try setting this to 50 or 100; iRacing - added new car class mappings; some internal rework and error handling improvements

Version 4.16.2.2: Fix a potential crash bug in the VR settings initialisation where config files are missing or corrupt

Version 4.16.2.1: Added transparency support to VR overlays; Some error handling improvements; iRacing - clear fuel when we don't actually need any and we're set to auto-fuel; GTR2 - plugin update; ACC - mapped some of the newer content; AMS2 - reinstate refuelling and next / previous pit strategy macros (note these assume that Q is bound to 'ICM Cycle' and the ICM Reset On Exit mode (under Gameplay) is set to 'Default Page'); A few minor bug fixes

Version 4.16.1.4: Added some additional logging for non-standard property values; Fix some incorrect 'opponent approaching' pit exit calls; ACC - wait a little longer for stable opponent positions before deriving player grid side and car-to-follow (should fix issues with occasionally incorrect formation lap calls); GTR2 - plugin update; A few minor bug fixes

Version 4.16.1.3: Clear fuel data in race session when starting from the pit lane (should prevent unrealistic fuel consumption figures in such circumstances); ACC - improved session restart detection; ACC - fix cut tracks playing after an invalidated out lap; ACC - recalculate car to follow and grid side at the start of double-file formation if drivers have dropped out; ACC - derive the pit window start and end times from the total pit window length, rather than the game-provided start and end times (should fix the incorrect window calls). Note that this may result in incorrect pit window calls if you enter a race session some time after the race has started

Version 4.16.1.2: Fix issues launching games; ACC - wired up more pre-start messages and some pit window and session transition fixes

Version 4.16.1.1: ACC - major improvements to data synchronization and stability work arounds. This should improve the ACC experience significantly, bringing it to a similar level to other games; AMS2 & R3E - open shared memory with read-access only. This should address some startup issues when using the app with other tools that access the games' shared memory files; GTR2 - plugin update; ARCA Sim Racing - added basic support for this game using the existing Rfactor1 plugin and classes; A few minor bug fixes

Version 4.16.0.1: Change target Dot Net version to 4.7.2. This should have no effect on users, it's required to support new collaborators on the project as the development tooling for the old 4.5 target version is very difficult to find; AMS2 - updated to latest shared memory version (includes IMO tyre temps and support for extra lap after timed races); iRacing - added DRS bleeps (enable with the "Enable DRS beeps" property); R3E - fix tyre type mapping for Gr5 cars; R3E - fix pit menu nagivation (note that this fix is required to keep up with changes to the game, and the 'fix body work' command is no longer available); GTR2 - plugin update

Version 4.15.3.12: Suppress "defend you position" type messages when we have a slowdown penalty; R3E - fix broken 'request pit' command and a couple of pit menu nagivation issues; R3E - added repair all and repair none voice commands ('pitstop fix all' / 'box, fix all' / 'pitstop repair all' / 'box, repair all' and 'pitstop fix none' / 'box, fix none' / 'pitstop repair none' / 'box, repair none'); GTR2 - plugin update

Version 4.15.3.11: Allow start and end chat actions to be disabled per macro; iRacing - fix for mapping errors when the game doesn't send us a player name; R3E - fix GTO pit menu tyre compound selection; Rf2 - pit menu refuelling bug fixes; AMS2 - added repair pit menu macros; GTR2 - plugin update

Version 4.15.3.10: Optionally block all complaining and negative messages - to implement this we've updated how we handle the "Max complaints per session" variable - this limits the maximum number of times the app can complain at or make a negative comment to the player in a single session. If you're happy to have the app complain when you do badly (by the app's occasionally unattainable standards) then set this to a higher number. If you don't want the app to complain at all set it to 0. The default value of 60 is intended to prevent the app's grumbling from getting tiresome in endurance or very long races; Added an option to limit the app's messages to information only ("Disable commentary" property, default false). This setting blocks the app's general 'advice' and encouragement / grumbling messages; GTR2 - updated plugin

Version 4.15.3.8: Content updates for iRacing and R3E; GTR2: Minor VR improvements; various bug fixes

Version 4.15.3.6: Revised pit window calculations; added a "Speak only when spoken to" option in the Properties screen (when this is enabled the app will only play messages in response to a voice command or button press); various bug fixes

Version 4.15.3.5: Fix MS Speech recognition platform download links; use 2 decimal places when reporting tyre pressure in kPa; iRacing - prevent autofuel triggering immediately after a driver swap and when the player isn't driving the car (thanks to Jacob Klien and Doug Evenson for helping track this down); GTR2 - plugin update; a couple of minor fixes

Version 4.15.3.0: Added a sector 3 pit warning when needing fuel; Added support for F1 2021 (spotter only, thanks to Christopher Berger); R3E - Work around the game's pit menu changes. The voice command "fix suspension" should work now but note that it will only toggle the value of this option (the app can't know what it's currently set to); GTR2 - update plugin to latest version; iRacing - fixed macro key presses; A few minor bug fixes

Version 4.15.2.9: Tweaked nAudio WASAPI playback to reduce the likelihood of sounds being truncated; fixed chat macro key presses not registering for some games; R3E & GTR2 - added dirt pickup warning messages (can be disabled with "Enable tyre dirt pickup warnings" property); GTR2 - damage reporting and gearbox improvements

Version 4.15.2.7: Fix a crash on start up if nAudio devices fail to initialise; AMS2 - added some missing content mapping; iRacing - added some missing content mapping; GTR2 - updated VR plugin, improved Driving School support and added some additional features

Version 4.15.2.6: R3E - updated WebHub to latest version with new data; iRacing - updated track landmarks data to add more tracks and correct some existing ones (thanks to Jacob Klein for the hard work here); RF2 - pit menu manager improvements; GTR2 - VR improvements

Version 4.15.2.5: Hotfix for broken chat macros (sorry it took so long); GTR2 - added windscreen wipers support and wired up formation lap logic

Version 4.15.2.4: Remove references to GoogleDrive in auto-updater data; Chat macro fixes; iRacing - added Spa Combined, Limerock Classic 2019 and Hockenheim GP corner mappings; ACC - added "how many laps on tyre set [x]" voice command (reports the number of laps this tyre set has been used for); ACC - added "[pitstop / box], select best tyres" voice command - uses the tyre usage data to select the least used tyre set in the pit MFD (just in terms of number of laps); ACC - damage threshold tweaks; R3E - fix some car class handling bugs when server use rolling start qualifying; A few minor bug fixes

Version 4.15.2.3: Change all links to thecrewchief.org to use HTTPS; Added option to disable frozen order (full course yellow / formation lap) messages (property "Enable frozen order messages"); Fix an app crash when some option files are corrupt or empty; Some fixes to the macro editor; ACC - fix some pit menu interactions - the app will always assume that the "Strategy" option is show so you'll need to ensure your current setup has at some pit strategies defined; R3E - use the tyre temperature ranges sent by the game instead of CC's internal ranges; GTR2 - added simple flatspot implementation

Version 4.15.2.2: Added support for Assetto Corsa rally tracks (use Assetto Corsa Rally game option); Added support for modifier keys in macros - prefix the key with modifier+ to hold down that modifier while typing the key (e.g. LSHIFT+KEY_X, RSHIFT+KEY_X, LCONTROL+KEY_X, ALT+KEY_X, etc); Adding an optional HOLD_TIME modifier to macro keys to specify how long that single key will be held down (e.g. "{HOLD_TIME,4000}KEY_A" hold A down for 4000 milliseconds); Added message playback resample option for nAudio - specify a resampling rate in Hz with the "nAudio playback resample rate" property - 0 disables resampling (the default). The sound files are recorded at 22050Hz so there's no point in going above that, values below 5000 may make the messages difficult to understand; Prevent key bindings activing macros multiple times after restarting the app; Some error logging improvements; Updated track landmark locations for a couple of tracks (thanks to the users who submitted these); A few other bit & bobs; RF1 / AMS - experimental implementation of pit box in use warnings (still lots of work and testing to do here, enable this with the "Warn about opponents sharing player's pit box (RF1 / AMS 1 only)" property, note this relies on participants making practice pitstops during practice / qual); AC - fixed some incorrect tyre wear thresholds; ACC - wired up tyre pressure and weather data (including weather forecast data); ACC - first cut of a pit menu manager (this will need updated macro definitions, please delete Documents/CrewChiefV4/saved_command_macros.json so the app can recreate it with the required content). The ACC pit menu manager uses cursor (arrow) keys and "P" to navigate the pit menu and can be a bit slow (to reduce the risk of presses being ignored by the game). Supported voice commands include "add fuel, [x] litres", "fuel to the end", "box, change tyres", "box, don't change tyres", "box, wet tyres", "box, dry tyres", "box, change [front / rear / left front / right front / left rear / right rear] tyre pressure, [20.3 -> 35]" (note that changing tyre pressures can be a bit hit and miss). In each case the app has to find and change the 'change tyre set' option to work out the menu state - it should revert the change it makes.

Version 4.15.1.5: iRacing crash hotfix

Version 4.15.1.4: Added option to prefer driver names over car numbers for frozen order calls (full course yellow / rolling start); Added suspension velocity histogram data to R3E, AMS2 and pCars2 chart data (default voice command "show me suspension velocity"); Made Windows speech recognition the default; R3E - fixed ratings retrieval; R3E - increase app tick-rate during start lights to reduce delay in reporting countdown / green flag state to other apps; GTR2 - fix some VR rendering issues; ACC - fix free chat macros; Various bug fixes and improvements

Version 4.15.1.2: Fixed some issues in gap reporting that could result in incorrect gaps or unwanted messages; Some internal speech recognition improvements; Automatically tune the speech recogniser's confidence thresholds - should improve recognition accuracy and consistency (this behaviour is enabled by default and can be disable by unchecking the 'Auto-tune speech recognition thresholds' property); GTR2 - some plugin and VR rendering improvements; RF2 - some pit menu fixes

Version 4.15.1.1: Added a configurable delay after releasing the speech recognition button before finishing speech recognition - may help users who tend to release the button too early ('hold button' mode only, property name 'SRE delay after button release'); Allow keyboard and button assignments to trigger macro functions and extended the Macro Editor UI to support key / button assignments; Speeded up free-chat macro functions; Extended mid-stage rally stage notes correction feature to include many more call types; UI enhancements (console context menu and other stuff); AMS2 - use separate command macros for AMS2 rather than reusing pCars2 macros; GTR2 - VR rendering enhancements and support for more message types including rolling start; RF2 - pit menu fixes

Version 4.15.0.6: Some error handling improvements - the app should show a popup window with error details instead of silently crashing and writing to the Windows Event Log; Command line parameter rework (note that the "RF2_64BIT" parameter has been changed to "RF2"); iRacing - some car class fixes; iRacing - fix some issues with off track penalties

Version 4.15.0.5: Added speech recogniser debug function (enable with 'Write speech recognition debug data' property) - writes recognition information and captured audio as a .wav file to Documents/CrewChiefV4/voiceRecognitionDebug/; RBR / Dirt / Dirt 2 - fixed a few bugs in the recce pace notes parser; GTR2 - fixed missing speech recognition commands;GTR2 - fixed missing reflections in VR; RF2 - pit menu manager improvements; Minor bugfixes

Version 4.15.0.3: Some timing accuracy improvements where opponent data is incomplete; RBR / Dirt / Dirt 2 - ensure corner / obstacle modifiers apply to the correct pace note fragment and fixed issue with pace note corrections not working; GTR2 - improved plugin handling; RF2 - pit menu tyre type matching improvements

Version 4.15.0.2: R3E - hotfix for error when the player has no rating data (i.e. has never raced on one of the official ranked servers)

Version 4.15.0.1: Added full support for GTR2, see https://thecrewchief.org/showthread.php?2012-GTR2-Setup-Instructions-and-Known-Issues&p=8180#post8180 for installation and setup instructions. Note that this also includes full (native) VR support for GTR2 (see the link for details); R3E - added rating-based finishing position expectation messages and voice command ("what's my expected finish position?"). The rating data also affects the race-end messages; RBR / Dirt / Dirt2 - rally stage notes creation improvements - added "into", "and" and "then" items, added "tightens to [1/2/3/4/5/hairpin]" command, ensure pace notes are ordered correctly, some other fixes and improvements; iRacing - added sanity checks to opponent speed and position data, should reduce the number of spurious stopped-car calls; Various bug fixes

Version 4.14.0.6: Fixed 'force single class' option (was being ignored by the app); Fix timing issue in multiclass races for some games; RBR / Dirt Rally / Dirty Rally2 - added 'Minimum rally distance pace note distance (metres)' property to set how far ahead the next pace note needs to be before the app inserts a distance call and 'Dynamic co-driver look ahead distance' property to shorten the look-ahead distance immediately after a slow corner (should reduce call overload into slow corners, disabled by default); RBR / Dirt Rally / Dirty Rally2 - added a few more corner / obstacle types ('open hairpin left / right', 'go-straight', 'danger / triple caution' and 'trees / logs / rocks inside / outside') and more corner modifiers ('opens', 'very long' and 'plus / minus') to the rally speech recogniser; R3E - added "What's the strength of field?" and "What are my incident points?" voice commands; iRacing - fixed missing meatball flag message; AMS2 - updated car mappings for new classes; RF2 - added more tyre types to the pit menu

Version 4.14.0.3: Changed 'My name' list behaviour - this UI component use to show the specially recorded 'personalisations' (about 1000 names) and all the opponent driver names in the app (about 10000 names), which made it unreliable for some users. By default it now hides the opponent driver names so you'll only see the 1000 specially recorded personalisations. Enabling the "Allow opponent names in 'My name' list" property will add all opponent driver names to this list allowing the app to generate a personalisation from any of the opponent names (see folder C:\Users\[user name]\AppData\Local\CrewChiefV4\sounds\driver_names). Note that these won't sound as natural as the specially recorded personalisations. If you've requested that your name be added to this list, please check again after updating as it might be shown; AMS2 - added missing car class mappings; RF2 - fixed tyre selection issues in pit menu manager when selecting some tyre types; R3E - fixed some missing incident points messages; Various minor bug fixes; RBR - automatically install the Crew Chief plugin

Version 4.14.0.2: Added rally stage notes creation support - see https://mr_belowski.gitlab.io/CrewChiefV4/VoiceRecognition_RallyStageNotes.html; Added support for Dirt Rally and Dirt Rally 2 (user-created stage notes only); Added option to enable scroll bars on main window where necessary (enable with 'Scroll bars on main window' property); Pit Manager - tyre selection handles in-game tyre compound that is not in PM's list; R3E - use correct field for player incident points

Version 4.13.1.7: Changed "watch" commands to be "monitor" ("monitor [opponent name / the car ahead / p12 / the leader / etc]" / stop monitoring [opponent name / etc] / stop monitoring all etc) - the word "watch" is too similar to "what's" so was being regularly mis-interpreted by the speech recogniser; Fix some issues with macros not applying to the correct application window; ACC - fix penalty logic; ACC - fix for the app repeating laptime in hotstint mode; RBR - subtitles fix; R3E - reduced optimial tyre temps for F-Junior; R3E - retry ratings downloads with http / https toggled

Version 4.13.1.6: Hotfix for iRacing crash bug (sorry guys)

Version 4.13.1.5: Added some interpolation for car timing positions - should address issues with wildly inaccurate gap messages where stale data (1 lap old) was being used to calculate gaps; ACC - fixed player car always being in a separate car class from opponents (should fix at least some of the inaccurate position calls); ACC - workaround for unsynchronised lap distance and laps completed data - should improve car position calls in online races

Version 4.13.1.3: Made splash screen optional; Some internal sound player wiring fixes; Don't attempt to show subtitles when there's nothing to show; Pit Manager "Clear all" clears tyre changes, fuel, any penalties and any damage repairs; Show separate speech recognition confidence thresholds for Windows and Microsoft speech recognisers on the Properties screen and ensure the thresholds used match the recogniser in use even in cases where the app has to override the user's preferred recogniser. Also added some additional logging to make it clear which recogniser is in use and what confidence thresholds are being applied; RF2 - Pit Manager changes tyres as part of tyre type commands (so you don't have to make 2 separate commands to change tyre type); RF2 - Fix tyre selection on Pit Manager for some cars

Version 4.13.1.2: Hotfix for startup crash for new users

Version 4.13.1.1: Added separate voice recognition sensitivity threshold values for when using the built-in Windows speech recogniser. These are higher (particularly for the 'Trigger word') and should stop a lot of the false-positives (again, especially for the 'Trigger word') when using the 'Prefer Windows speech recogniser' option; Fix z-offset not saving for some VR overlays; Allow the 'Watch opponent' function to be disabled (added 'Enable watch opponent commands' property, default enabled); Fix bug playing start-listening beep using Trigger Word voice recognition mode and nAudio; Added shortcut key bindings to toggle VR overlays; Added option to control when subtitles are loaded. If the property 'Lazy load subtitles' is enabled the subtitles are loaded only when needed. This can improve app launch times when using subtitles but may increase app resource use when driving; R3E - fix for incorrect session length message with time + 1 extra lap race sessions; iRacing - added Meatball flag message and generic pit-to-serve-penalty message (the app doesn't know if it's a drive-through or a stop-and-go penalty); RF2 & RBR - fix app crash when restarting the app

Version 4.13.1.0: Fixed some cases where the app would use text-to-speech driver names when it shouldn't when the TTS settings was 'Use TTS only when necessary'; pCars3 - always assume races are single class; R3E - added support for DTM 2020 rules (push to pass and DRS); R3E - added support for minimum pit stop duration (calculates how long you need to wait in the pitbox and warns you about it - note that this calculation requires you to have driven out of the pits at least once since starting the app to record the track's pit exit location); R3E - added support for maximum incident points on ranked servers before being kicked (warnings about further incidents resulting in a DQ / kick); R3E - reduced some engine temp warning thresholds; RF2 - enhanced pit menu manager to support fuel in gallons and some other fixes

Version 4.13.0.2: VR overlays: Fixed gazing. Added an option to change tracking space for overlays (Seated/Standing/Follow Head); Added an option to only use sounds which are gender-neutral. The app has, historically, assumed all players are male so contains lots of sounds with words like words like 'man', 'guy', 'he' etc which is a bit silly. If you enable the 'Block sounds which refer to drivers as male' property the app will only use sounds that are gender-neutral ('driver', 'car', etc). Note that this requires the latest sound pack update to work; RF2 - tweaked the behaviour of the pit manager system so it only inspects the pit menu layout when it's first invoked, rather than at the start of a session; RF2 - added an option to disable the pit menu manager ('enable rF2 pit manager' property); RF2 - fixed initialisation of pit menu manager

Version 4.13.0.0: rFactor 2 now needs Vytautas Leonavičius' rFactor 2 Plugin version 3.7.14.2  It's part of this CC version and will be installed automatically but note that any other apps that use it will need to be updated; Added Rally mode and support for Richard Burns Rally; Added rudamentary pCars3 support; Added support for controller 'point of view' buttons (should allow direction-pads to be used for button assignments); AMS2 - A few car class tweaks; ACC - added multi-class support; RF2 - updated plugin and added a pit menu manager

Version 4.12.0.8: Corrected VR overlay default behaviour (should fix the remaining issues with the overlays); corrected app name being "5" on the Windows Sounds mixer (stupid fat-fingered typo, sorry); added "what's my oil temp" and "what's my water temp" voice commands; R3E - tweaked car class mapping for WTCR

Version 4.12.0.7: Hotfix for issues with VR overlay windows; corrected time deltas in multiclass racing on pCars2 / AMS2; R3E - fixed some missing tyre type mappings

Version 4.12.0.6: R3E - fixed OtterHud integration

Version 4.12.0.5: Some UI tidying up and bug fixes; Stockcar Extreme - fix for app trying (and failing) to install plugin on every start (the plugin .dll must be install manually); R3E - download player rating data in background (should improve start up time); pCars / pCars2 - allow finer control over car class mapping using an optional pCarsCarNames field in the car classes JSON; AMS2 - wired up a bit more of the existing pCars2 logic (e.g. weather conditions); added F1 2020 to games list (spotter only); fixed some VR integration bugs

Version 4.12.0.1: Fixed SteamVR detection on non-English installs; Fixed some issues in speech recognition initialisation error handling; Fix a crash bug preventing the app from starting when user-defined macros are incomplete; Allow existing driver name (opponent name) recordings to be used to generate personalisations (note that these may sound a bit robotic - some will probably be fine, some will be unusable, most will be somewhere in between). If you're waiting for your name to be added to the app check the My Name list again - it might be available; Tweaked the My Name list box so you can now type into it (this is needed because there are now about 8000 entries in this list); Added option to force VR overlay windows to be on top; A few minor bug fixes

Version 4.12.0.0: Added support for Steam VR overlays. Enable this with the 'Enable VR overlays' property (the app can also start SteamVR if necessary by enabling the 'Start SteamVR if detected' property). This will enable the 'VR Settings' button on the main screen which opens a popup window allowing you to select which of your desktop windows to be rendered in VR. You can also move them around, scale them and other stuff from this menu. This supports any desktop window as well as the Crew Chief overlays (charts, console and subtitles). The app will remember your window config and reapply them when you next start it. If you want the VR overlays to be started with the app, enable the 'Enable VR overlays when app starts' property; Reworked help and documentation into proper HTML pages accessed from the app's toolbar menu; Overhauled button handling to reduce delays and fix some issues; Some pace notes improvements - better multi-lap pace notes handling and add better audio feedback (pace notes specific voice message and a different radio beep when recording mode is enabled); Added feature to 'watch' opponents - use the voice command 'watch [opponent last name]', 'team mate [opponent last name]' or 'rival [opponent last name]' to mark a driver as watched. The app will give updates on watched drivers. The watch list can be cleared with the 'cancel watched drivers' command (or 'stop watching [opponent name]' to remove a specific driver). Note that the watch list is cleared on session start so drivers watched in qualifying won't be on the watch list for the race (you need to re-add them); R3E - added support for R3E driver rating data. When approaching an opponent with low reputation you may get a warning about him. Opponents' ranking, rating and reputation can be accessed with the voice command 'whats [opponent last name]'s ranking / rating / reputation'; R3E - updated various vehicle classes and their tyre temperature thresholds and added Daytona track mapping (running on the oval should now activate the oval spotter and logic); Various bug fixes

Version 4.11.1.2: Reworked pace notes functionality to allow pace notes to be recorded over multiple laps and to add some speed and direction filtering (e.g. you might want a particular pace note to only play when the car is going too fast) - full documentation still to be done; added option to make pace notes play automatically when starting a practice session (disabled by default, enable with 'Play pace notes automatically in practice' property); added option to mute other messages when pace notes are playing (enabled by default, disable with 'Mute messages when playing pace notes' property); improvements to the subtitle overlay; replaced existing 'fuel in gallons' property with more generic 'Use metric' property which applies to fuel and speed calls; added option to disable pit speed limit warnings ('Enable pit speed limit warnings', defaults to enabled); iRacing - fix some pitstop commands; AMS2 - fix command line wiring; R3E - fix broken opponent position messages when asking "where is [opponent name]"; R3E - added some missing tyre types to the pit menu code; R3E - tweaked tyre wear warning messages so the app isn't as conservative; ACC - fix for broadcast API change

Version 4.11.0.3: Fix some subtitle issues when using personalisations for some users; allow sounds to be played back in stereo when using nAudio for playback (enable with the "Play messages in stereo" property) - should fix cases where the app's sound only plays through one speaker; ACC - changed deprecation warning so it's just a console message rather than a popup; changed default subtitle overlay colour scheme (this can be changed manually by editing the Documents/CrewChiefV4/subtitle_overlay.json file - the recommended value for the "activeColorScheme" property is "CrewChief")

Version 4.11.0.2: Added subtitles. Subtitles can be shown as an overlay from Crew Chief (enable with the 'Enbable subtitle overlay' option in the Properties screen) or can be written to a shared memory file for use in 3rd party overlays or dashboards (enable with the 'Enable shared memory' option). See https://gitlab.com/mr_belowski/CrewChiefV4/-/blob/master/CrewChiefV4SDK/CrewChiefV4SDK/Program.cs for an example project which uses this shared memory file. Note this subtitles require the latest sound pack update. Massive thanks to Nolan Bates for a frankly astonishing amount of work transcribing all of Crew Chief's phrases; Initial Automobilista 2 support (work in progress). To use this set the Shared Memory mode to Project Cars2 in the in-game System menu; ACC - removed pit window messages; ACC - added deprecation warning. We're stopping work on ACC support and might remove support entirely in the future; various bug fixes and tweaks

Version 4.10.0.9: Better handling of corrupted settings files on startup; improved detection of on-track incidents; R3E - tweaked suspension damage thresholds; iRacing - improved session state detection when racing against AI; iRacing - fix pit limiter warnings

Version 4.10.0.8: Fixed a couple more start up crashes; fix some queuing issues in more complex rally-style pace notes; RF2 - added Nurburgring corner mappings; R3E - work around some odd behaviour in the pit menu for races with more than one pitstop; a few minor tweaks and fixes

Version 4.10.0.6: Fixed a couple of crash bugs on start up with broken profile settings; Tweaked pace notes feature to allow a set of sounds to trigger instead of a single sound (to better support complex rally-style pace notes); iRacing - some AI session restart logic fixes; R3E - updated some car class data

Version 4.10.0.5: Reworked cut track messages to make them more appropriate to how often you cut / violate track limits - note that if you cut persistently and frequently the app will (eventually) start to ignore these cuts; R3E - auto-select 'serve penalty' in the pit menu when you issue the 'box this lap' command with an outstanding penalty (requires the R3E pit menu key bindings to match the ones the app is expecting); R3E - fix crash bug caused by null or empty driver names when sending WebHud data; iRacing - allow the app to manage disk telemetry handling (enable with 'Enable automatic telemetry disk recording' property); iRacing - fix some issues with the track map on chart overlays; iRacing - improved cut track detection

Version 4.10.0.3: RF2 plugin hotfix; Added track map to charts when zoomed in (uses car position data) - visible area is hightlighted in red 

Version 4.10.0.2: Chart controls hotfix

Version 4.10.0.1: Added overlays for console output and telemetry charts. See Overlays section for more info; some audio caching improvments; some minor tweaks and fixes; RF2 - new plugin 

Version 4.9.11.3: Hotfix for app startup issues using nAudio where device names aren't unique

Version 4.9.11.1: Reworked nAudio playback path to improve stability and fix some app shutdown issues; added option to play sounds using WASAPI output device when using nAudio playback (set with the 'nAudio Output Interface Type' property). This has significantly lower playback latency and can make the app sound more natural and responsive - definitely worth trying (remember to enable nAudio playback first). If the audio pops or crackles with this enable try increasing the 'nAudio WASAPI latency' property a bit; added option to make the app respond to voice commands as soon as it recognises them in 'Hold Button' mode, rather than waiting until the radio button is released (enable this behaviour with the 'Respond to voice commands immediately' property); Allow closing CC from command line by passing C_EXIT command; ACC - ensure penalties are cleared properly; AC - some car class tweaks to better map GTE and GT3; iRacing - more robust pit entry detection (should reduce the likelihood of the all not refuelling after a mess pit entry); A few bug fixes

Version 4.9.10.1: Prevent fuel consumption estimates being skewed by full course yellow flags; Reworked damage reporting logic to make it more natural; Some message queuing improvements; Disable opponent pit exit position estimates during full course yellow flag; Allow pace notes to play even when the app is in 'keep quiet' mode; Play a beep when releasing the radio button in 'Hold button' mode (disabled by default, enable with 'enable_on_hold_close_channel_beep' property); When spotter messages interrupt the chief or you press the radio button, any sound currently playing is interrupted immediately (only works with nAudio playback enabled and, for voice communication interrupting, 'Block messages when talking to the Chief' enabled); Fix 'toggle mode' speech recognition button press issues caused by the app seeing multiple presses when activating speech recognition; Fix nAudio device indexing when changing default devices; ACC - added driver stint time messages, more penalty messages and some other missing data; F1 2019 - use correct property value for UDP port (was using the value for the F1 2018 property); R3E - improved detection of hot-lap qualifying sessions to prevent spurious spotter messages; R3E - fix missing tyre wear messages when multiplier is 2 or more; R3E - fix incorrect DRS messages

Version 4.9.9.5: Final hotfix (fingers crossed...) for the remaining speech recognition initialisation issues

Version 4.9.9.3: Another hotfix (sorry guys) - fix broken speech recognition with non-English versions of Windows, added more sanity checking for user profiles

Version 4.9.9.2: Hotfix - fall back to Microsoft speech recognition engine implementation if the System speech recognition engine doesn't have the required language support; Hotfix - fix a crash bug when initialising the speech recognition system for nAudio users; Hotfix - fix plugin location error

Version 4.9.9.1: Reworked speech recognisers to allow it to use the built-in Windows speech recognition, which may benefit from being trained to you voice. To enable this, enable the 'Prefer Windows speech recogniser' option in the Properties screen; Added experimental free-text chat feature for Raceroom, pCars2 and iRacing only - enable with 'Enable free dictation chat messages' property. To use this you must have Windows speech recognition enabled and you'll need to delete Documents\CrewChiefV4\saved_command_macros.json before launching the app (so the app can add a couple of new macro definitions). Read what out you want to say as you would with any other command, starting with "chat" - e.g. "chat, hello everyone" or "chat, this is a test chat message". The app will start the chat by executing the "start chat message" macro which presses C (raceroom) or T (iRacing / pCars2), type in the recognised text after "chat", then end the chat by executing the "end chat message" macro (which just presses enter). Note that this may produce some weird results if the speech recogniser doesn't accurately interpret what you're saying; Added a voice command to disable most of the complaining messages for the remainder of the current session - "stop complaining" / "stop grumbling" / "f*** off". This will prevent the app berating you when you're doing badly, which may be useful in long sessions; Limit the number of times the app will complain at the player during a session (default is 50 times, configurable with 'Max complaints per session' property); Fixed app crash when using pCars network data button assignments
 
Version 4.9.8.24: ACC - fix stale opponents not being cleared from internal state (should fix incorrect incident calls when players disconnect); ACC - mapped corner positions for Spa and Barcelona, corrected Monza mapping; R3E - added missing WTCR 2019 class (this is now correctly grouped with WTCR 2018)

Version 4.9.8.22: Fix for updates requiring 2 restarts in order to correctly load the user's settings; ACC - fix various issues including multipler bugs, missing pit exit / entry messages, incorrect mandatory pit stop window messages, missing flag messages, missing track landmark mappings (not every track yet) and a few other bits and bobs; iRacing - fixed a nasty bug where a particular set of unexpected car class data from the game could make the app unresponsive

Version 4.9.8.20: Added F1 2019 support (spotter only); Added Assetto Corsa Competizioni support. This is a work-in-progress - the studio is still working on the API and there are issues with some of the data and some features of the app don't yet work; Added properties profiles to allow sets of options to be saved and reloaded - different profiles can be created with different app configurations. These can be created and loaded on the Properties screen, and can also be loaded at app start time with a command line argument by specifying 'profile profile_file_name.json' in the shortcut (e.g. short-cut Target: "C:\Program Files (x86)\Britton IT Ltd\CrewChiefV4\CrewChiefV4.exe" profile some_profile.json); Allow some properties to be modified without needing to restart the app; Fixed an issue with missing opponent gap data when the player is running 2nd; Added option to enable blue-flag messages ("Enable blue flag messages", defaults to enabled); Added option to limit the games show in the Crew Chief 'Games' list - to use this add a comma separated list of the games you want to be show to property 'Limit available games'. Most common versions and abbreviations of the game names should work here (e.g. "R3E, pCars2, Assetto, ACC, RF2"); Allow radio beeps when the Chief talks to be switched off ("Enable radio beeps" properrty, defaults to enabled); R3E: extended pit menu interactions - you can now ask for a specific tyre type in car classes that support it, for example "box, soft tyres" (see R3E Pit Menu Interactions)

Version 4.9.8.8: Fix potential crash bug when assigning buttons or when testing button assignments / voice commands before the app receives any game data; removed some debug code that may have triggered in the previous release

Version 4.9.8.7: Rewrote installer to use WIX rather than InstallShield - should help with the updating issues that some users encounter; added an optional delay before switching the spotter to be switched off on full course yellow (default is to silence spotter as soon as the full course yellow is shown - to use the new behaviour disable the 'Mute spotter immediately on full course yellow' property); fix crash bug when disabling 'Identify opponents by race position'; assign button to action when button is released, not pressed - should fix issues with devices which keep buttons pressed all the time; RF2 - added latest Formula E mappings and rules; R3E - improved accuracy of pit box countdown; 

Version 4.9.8.6: Assign buttons when the button is released, not pressed. Should fix issues where continuously pressed buttons prevent button assignment from working; RF2 - added ignore-blue-flags warnings; R3E - added 'box, next tyre compound' voice command to cycle through available tyre types in the pit menu (see R3E Pit Menu Interactions section); R3E - added TireLoad data to WebHud export and incremented version number

Version 4.9.8.5: R3E - corrected opponent tyre mapping for F1 and GroupC cars; R3E - make use of pit menu data to provide more control over pit menu - added voice commands like "box, change front tyres only", and "box, don't fix aero". See R3E Pit Menu Interactions section here and in Help for more information; a few minor fixes

Version 4.9.8.3: Prevent the app spamming the console window with errors when something fails on every tick (should prevent crashes when something goes wrong); in 'keep quiet' mode (e.g. after telling the app to "shut up") it really does keep quiet - even high priority messages are blocked - only the spotter messages continue to play (these can be blocked with the "don't spot" command). If you want the old behaviour back, where 'keep quiet' mode still allows high priority messages to play, enable the "Play important messages when silenced" option; added a UI to add voice messages to the list of available button actions so you can easily assign a button to what would have been a voice command - press the 'Add / remove actions' button on the main screen to access this; added some missing validation on the good / bad start messages

Version 4.9.8.2: Fixed app hang when using Trigger Word speech recogntion with nAudio; Allow nAudio speech recognition input device sample rate, bit depth and channel count to be set in Properties; Added some additional checks to prevent gap messages being played immediately after an overtake; AC: don't load opponents into the speech recogniser once a race has started (should reduce CPU spikes in online races); AC: fixed inaccurate laps-remaining data; iRacing - fixed some missing messages; various bug fixes and some code tidying up

Version 4.9.8.0: Start listening as soon as the radio channel button is pressed, instead of waiting for the beep to finish playing; iRacing - revised fuel calculations in races with driver swaps to use highest per-lap consumption - should fix refuelling being wildly inaccurate due to missing consumption data when your team mate is driving the car; iRacing & R3E - extended the speech recogniser to include opponent car numbers - you can now ask for opponent data using car number, e.g. "what's car number 10's best lap time", or "where is car number ninety nine". To use this, enable the "Identify opponents by car number" property. Note that for iRacing car numbers may have leading zeros - in cases where a given number appears more than once in the session (with and without leading zeros), the speech recogniser will expect the leading zeros to be used in the command (i.e. car number 021 can be referred to as "zero twenty one", "oh twenty one", "zero two one" or "zero twenty one" in all cases, and "twenty one" or "two one" only if there's no other car in the session with number 21); iRacing - Allow car numbers with leading zeros to be read by the full course yellow / formation lap code (e.g. car 009 will now be read as "zero zero nine"); Reduce the maximum frequency of opponent tyre change messages to prevent spamming when many cars pit for different tyres at the same time

Version 4.9.7.9: Fix for some potential start up issues; fixed toggle_mute button function not working properly; RF2 - new plugin version and some additional DRS messages

Version 4.9.7.8: Hotfix for spotter being always set to 'ovals' mode on road courses (missing beeps, some other changes - sorry guys). Ovals mode will now only be enabled on ovals, as intended; fixed nAudio beeps not responding to volume changes; added new button binding for 'Toggle mute' - mutes all app sounds and unmutes when pressed again

Version 4.9.7.7: Change spotter behaviour on ovals to prevent the spotter monopolising the radio channel (non-spotter messages aren't blocked all the time). This disables the radio beeps on ovals and starts spotting immediately on race start. Note this affects oval tracks only. You can revert to the old spotter behaviour by disabling the 'Enable enhanced spotter on ovals' property; fixed some button assignments not working until the app was restarted; fixed broken pit stall count down; fixed some broken validation that was preventing gap ahead and gap behind messages playing in some circumstances; fixed broken 'play corner names' command; allow voice commands that refer to opponent cars (e.g. "what's the car ahead's last lap time") to be assigned to buttons - you need to add opponentDataCommand: true to the buttonAssignment element in controllerConfigurationData.json in these cases

Version 4.9.7.6: Prevent fuel estimates playing too close together; fixed cases where the app switches to multi-class mode when it shouldn't - when there are too many unknown car classes in a session the app now reverts to single-class mode as intended; AMS / RF1 - update race start positions shortly after the green light - should fix cases where the 'bad start' message was playing when it shouldn't

Version 4.9.7.5: Hotfix for iRacing crash bug

Version 4.9.7.4: R3E - added support for WebHud so Crew Chief can send data to WebHud in place of dash.exe. Enable this with the 'Enable WebHud integration (R3E only)' property. This should also be slightly more efficient that the old dash.exe program; don't play laptime or pace report for laps which are several seconds slower than your pace; allow fuel consumption calculations to be based on the lap with the highest representative fuel usage - this should produce more accurate fuel calculations on ovals and in cases where there's just been a full-course-yellow / caution period or other event that can significantly reduce fuel consumption. By default the app will use max per-lap consumption on oval courses and a windowed average consumption on road courses. Enable 'Base fuel calculations on max fuel consumption' property to use max per-lap consumption on all tracks; iRacing - reworked session position logic to reduce inaccurate position calls; iRacing - additional messages for formation laps and full course yellows; a few assorted fixes

Version 4.9.7.3: R3E - added support for new shared memory layout introduced in latest game update

Version 4.9.7.2: RF2 - incorporated more stock car rules and added some additional car class mappings; RF2 - fixed some issues introduced in last RF2 game update; some minor bug fixes

Version 4.9.7.1: iRacing - map the 'pits are open' flag and added BMW M8 car data and a couple of Okayama layout corner mappings (still some missing recordings here); RF2 - various tweaks including lone practice support and stock car improvements; R3E - mapped Sepang and Nordschleife corners (VLN and Nords layouts only) - might not be very accurate (it's a big track and the available maps aren't great)

Version 4.9.7.0: Don't play lap time comparisons during full course yellow; fixed button assignments being ignored when Voice Recognition Mode is set to 'Press and release button'

Version 4.9.6.9: Minor bug fixes; AC: ensure tracks always have 2 or 3 sectors - this *definitely* fixes long-standing issues with some longer add-on tracks that report 4 or more sectors

Version 4.9.6.8: Some stability fixes

Version 4.9.6.7: Fixed nAudio speech recognition; some more minor improvements to the controllers code

Version 4.9.6.6: Simplified macros file format (removed the per-game key mappings) and added a UI to allow macros to be created and edited from the app. Important: if you've created your own macros or have been helping us test our recent beta releases, please rename Documents/CrewChiefV4/saved_command_macros.json before starting the app. See Command Macros section of help.txt for more info; re-wrote controllers code to cope with badly behaved devices (device scans that take many minutes and devices that disconnect and reconnect during play). Controllers are no longer scanned at start-up (press Scan for Controllers to initiate a device scan); moved saved button assignments to Documents/CrewChiefV4/controllerConfigurationData.json. This has been extended to allow any of the existing basic voice commands to be mapped to a button - see Button Mappings section in help.txt for more info; allow cut track warnings to be enabled / disabled with a voice command ("enable cut track warning" / "no cut track warnings") or a button binding ("Toggle cut track warnings on / off"); AC - another attempt to fix weird crash on tracks with > 3 sectors; RF2 - updated plugin to include stock car rules logic and added more stock car (oval) specific messages; RF2 - added more messages for various penalties and other conditions; some simple camber and pressure responses using average inner-middle-outer tyre temps recorded during the previous lap (voice commands "how are my [front / rear] cambers" / "how's my [left front / right front / etc] camber", and "how are my tyre pressures" / "how are my [front / rear] tyre pressures"). You can also add "...right now" to this voice command to get the current inner-middle-out temps (not averaged). Note this isn't well tested, might give bad advice, and doesn't work with iRacing

Version 4.9.6.5: Speculative fix for startup error in previous version; added R3E GT Masters 2018 car class to GT3s; AC - fix crash on tracks with > 3 sectors

Version 4.9.6.4: Automatically scan for controllers on app start; better handling of controllers; disable some irrelevant messages on in-laps; RF2 - better (more detailed) penalty messages; RF2 - updated plugin with more data and additional features not exposed via official rF2 Internals API. These are turned off by default - to enable them see 'rFactor2 Unofficial Features' section above

Version 4.9.6.3: Tweaked manual rolling start logic for multiclass races, so each class has its own pole-sitter for determining when to start the race (can be disabled with the 'Manual formation lap separate classes' option); significantly reduced the size of debug traces; reworked console logging output to fix some potential crash bugs; iRacing - potential fix for incorrect race start position in multiclass races (this caused quite a few issues)

Version 4.9.6.2: Hotfix for iRacing crash

Version 4.9.6.1: Extended free-text macros a little (upper case characters and slash); prevent off-track messages expiring immediately in practice sessions; speech recognition fixes when running with 'disable alternate voice commands' switched on; automatically save console logs; iRacing - fixed debug logging on ovals; RF2 - some bug fixes

Version 4.9.6.0: Corrected some car classes for AC and R3E; RF2 - updated plugin; RF2 - more accurate pit stall location detection

Version 4.9.5.9: Notify when given slowdown penalty; AC - fix crash when restarting app; a few minor fixes

Version 4.9.5.8: Some fuel calculation tweaks - near the start of the race, use the max per-lap consumption when calculating required fuel; Save pitstop benchmark times for each car / track / game combination so they persist between runs of the app (written to /Documents/CrewChiefV4/pit_benchmarks.json) - this is enabled by default but can be disabled with the 'Save pitstop benchmark times to disk' property; RF2 - workaround for missing spotter calls after driver swaps (thanks to kcr55 for this one); RF2 - use tyre compound name rather than index when deriving tyre types - should fix issues where mods declare different available tyre compound sets; pCars2 - make macro key presses a bit longer so they're less likely to be missed by the game (note this makes the refuelling macro execute quite slowly)

Version 4.9.5.7: Fixed RF2 tyre type mapping and added Hyper-Soft tyre; use correct 'drizzling' sounds; added spotter 'Florian' (in latest sound pack update)

Version 4.9.5.6: Don't allow some messages to be inserted into the space between spotter messages; some work to prevent stale messages being played (e.g. after a delay caused by the spotter); don't play fuel warnings when on a low-fuel run in qualifying or practice; prevent some position messages being played twice; reduce repetition of some messages; ignore rear tyre and brake temperatures in FWD classes; more internal threading fixes; a few other tweaks and fixes

Version 4.9.5.5: Macro overhaul - more flexible system which should be easier to work with. Includes example chat macros and an 'add fuel, XXX litres' macro (where XXX is 0 - 150) (R3E and pCars2 only). See the 'Advanced command macros' section of the help.txt file or https://thecrewchief.org/showthread.php?263-Command-key-press-macros&p=2378&viewfull=1#post2378 for more info; fixed some shutdown delays that can happen when closing the app without pressing 'stop' first; fixed radio channel sometimes being left open after a spotter call; fixed brake and suspension damage not being reset after repairs; added option to play pit box distance messages in feet ('Pit box distance countdown in feet'); added option to play a 5-4-3-2-1 style pit box count down ('Pit box time countdown' property, disabled by default). Reaches zero 30 metres from the pit stall by default (configurable with 'Pit box time countdown end point (metres)' property) - note that this may be inaccurate if the pit box is close to the start line due to the way lap distances work when in the pit lane, especially on tracks where the pitlane is significantly longer or shorter than the racing surface; make volume control sliders more fine grained to allow better control over volume; RF1 / AMS: revised cut track logic - should reduce cut track warnings; pCars2 - 'request pit' macro now checks the pit request state from the game and responds accordingly; pCars2 - added "cancel pit request" macro; internal bug fixes

Version 4.9.5.3: Internal overhaul for better performance and stability. Fixes some threading / resource issues that could cause instability on shutdown, changes to how the internal event processing works to make it faster and less resource-hungry, removed 'disable immediate messages' option as this is no longer needed, reduced CPU overhead in lots of other places; fixed app crash when failing to initialise nAudio background sounds player; pCars2 - added 'pCars 2 spotter car length' property, default 5 metres (app was using whatever had been set in the pCars1 version of this property)

Version 4.9.5.2: Fix laps-to-go calculation - it's now based on the number of laps completed by the overall leader so should be correct when you've been lapped; allow for changing conditions when determining the player's pace and best lap time - comparisons are only made to laps completed in broadly similar conditions and best lap set in the current conditions is used when working out things like expected laps remaining and required fuel loads; fixed rounding errors in fuel calculations that could accumulate to substantial amounts; use overall fuel consumption when determining how much fuel to add, rather than recent fuel consumption (the app sometimes under-fuelled if the player was fuel saving in the laps before his pit stop); R3E - fixed some missing state transitions; pCars2 - use updated RainDensity data for more fine-grained rain calls; some performance improvements

Version 4.9.5.1: Added support for F1 2018 *spotter only*. This uses the UDP data stream sent by the game - the app listens on the default port 20777 (can be changed with the 'F1 2018 UDP Port' property). This is an early implementation so expect some bugs - please report them. Again, it's only the spotter - the app doesn't interpret any of the other game data so it won't give other calls or be able to answer any questions. I may add full support later, but as the crew chief built into the game is actually pretty good this isn't a high priority; RF2 - updated plugin to latest version.

Version 4.9.5.0: Some fuel calculation fixes; reduced likelihood of yellow flag warnings for cars that have already quit to pits; added fuel-window messge in races which have no mandatory stop but will need a single fuel stop; some internal fixes; iRacing - fixed issues with opening lap data

Version 4.9.4.9: Added alarm clock function - say 'set alarm at twenty oh five', for example or preset it in properties "Alarm time(s). "Enable alarm clock voice commands" must be enabled before the alarm clock voice commands works; alternate sound pack internal rework; mark abandoned laps as invalid; more ghost detection improvements; R3E - removed 'confirm penalty' macro as this is no longer needed and can cause issues; reworked fuel-to-end reserve calculation - you can now specify a number of laps worth of additional fuel to add as a reserve when fuelling to the end of the race. The 'Additional fuel to add to finish the race (number of laps worth)' property can be a whole or fractional number of laps; various bug fixes

Version 4.9.4.6: Enable some of the multiclass warnings in qualifying and practice sessions; allow the wait timeout for Trigger Word speech recognition mode to be set ('Trigger word wait timeout', default 5 seconds). The app will wait this many milliseconds for a command after hearing the trigger word before giving up; fixed some broken personalisations from the last update (a few of the personalised messages weren't loading); iRacing - better 'oval' detection (should prevent the app using oval spotter sounds on some road courses); iRacing - allow 'add fuel' voice commands without a unit (e.g. "pitstop add 12") - will use whatever fuel unit the app is configured to read fuel amounts in

Version 4.9.4.5: Added 'Trigger word' voice recognition mode. With this enabled voice recognition works a bit like 'OK Google' or Alexa - the app listens for a special keyword or phrase (configurable in the 'Speech recogniser trigger word' property, default value 'Chief'). When it hears the keyword it starts listening for a regular voice command for a few seconds. This doesn't work with nAudio input; some corner name mapping fixes; changed default interrupting behaviour so only spotter messages interrupt the Chief; only consider rear wheels as locked if both are locked at the same time in common FWD cars like WTCC and WTCR (prevents the app warning about locking the unloaded inside rear on turn-in); when requesting fuel estimates for a given time or lap count, the app now reads back the time / lap count you requested along with the fuel estimate (e.g. "for 25 minutes, we estimate you'll need about 30 litres"); R3E: reworked hotlap single practice mode detection - should prevent spotter playing in these game modes; various internal fixes

Version 4.9.4.3: Fixed a few cases where messages play when they shouldn't; reduce repetition of blue flag messages; RF2 - fix 1970s F1 class mappings; iRacing - use average temp round track now this has been updated in the SDK to be dynamic

Version 4.9.4.2: Prevent unimportant messages playing if the player is fighting with other cars in a race session or is on a flying lap in a qualifying session (disabled by default, enable this with the 'Advanced message prioritisation' option); allow app update check to be skipped - adding the start argument SKIP_UPDATES to your shortcut will prevent the app checking for updates (but it'll still check for sound pack updates); report yellow flag messages even if we're in a corner / braking zone; various bug fixes and tweaks

Version 4.9.4.0: Fixed regression in non-English speech recognition config files - the 'defaultLocale' property name for speech recognition language had been changed to 'language', so the app was ignoring this value in existing configurations. The app will now use the 'defaultLocale' property value if it doesn't find a 'language' property

Version 4.9.3.9: Allow spotter messages to play if they have interrupted another message, even if they would otherwise have expired; iRacing - enable fuel tracking in lone practice sessions; iRacing - Added option to re fuel to end of race in a race session when entering the pitlane, this will not trigger if you get a tow back to pits it only works for regular preformed pitstop!; iRacing - some multiclass fixes; pCars2 - performance improvements; internal rework - consolidated lots of timings code to make behaviour more consistent across games; Speech recognition engine initialisation improvements - better handling of language / country options, better management of installed components and error reporting; improvements to interrupting logic; various bug fixes

Version 4.9.3.7: Added option to pause most messages in braking and cornering zones (sections of the track starting from heavy braking and ending with near-full throttle application) - this doesn't apply to spotter messages or other very high priority messages. It can be enabled and disabled with a button mapping ('Toggle delay messages on hard parts') or a voice command ('don't talk in the [corners/braking zones]' / 'no messages in the [corners/braking zones]', 'talk to me anywhere' / 'messages at any point'). This feature is switched off by default - the initial state is set with the 'Delay messages on hard parts' option. Note that you need to drive at least one valid lap before the app can work out which sections of the track it needs to keep quiet in. This feature is disabled for ovals and may produce odd results on very short tracks; added some basic message prioritisation - higher priority messages will be inserted into the queue in front of lower priority messages; replaced the 'Spotter and responses block other messages' property. The spotter (and some 'critical' messages) now interrupt and block regular messages (most things except voice command responses and some other important messages) by default. Enable the 'Command responses and important messages block other messages' to allow other types of important message (such as voice command responses and yellow flags) to also interrupt and block messages; prevent any further fragments of a message from playing if this message was interrupted; simplified pitstop benchmark process - you can now get a benchmark pitstop time without having to set a hot lap first (although you'll still need to set one after your stop to allow the app to work out the time loss); a few bug fixes

Version 4.9.3.6: Reduce size of 'personalisations' update pack (install this update before updating your personalisations); Much more accurate tyre life prediction using a non-linear curve fit of tyre wear data recorded for the current tyre set (assumes a tyre is worn out when it has 5% life left); added more info to the fuel status message - now includes fuel-to-end; allow spotter sounds to cancel other queued messages (disabled by default, enable with 'Spotter and responses block other messages' property - note this can sound strange when only the start or end sounds of a message play); a few minor tweaks; iRacing - fix white-flag message spam

Version 4.9.3.5: Fixed sound pack version check

Version 4.9.3.4: Changed how sound pack downloads are handled - should significantly reduce the size of sound pack updates; added property categories to make finding stuff easier; added opponent pit exit prediction warning - if the app thinks an opponent car will probably come out of the pits close to you (and it can read his driver name), you'll be warned about him; further opponent pit exit estimation improvements; some internal bug fixes

Version 4.9.3.3: Fixed opponent pit time calculation in pit strategy - app was incorrectly guessing the time an opponent car was in the pitlane; take mandatory pitstop status into account when deriving expected post-pit opponent positions (assume opponents who still haven't pitted will pit this lap if we're at the end of the pit window); Added and corrected a few car classes (pCars2, iRacing and R3E); iRacing - fixed some car class allocation issues

Version 4.9.3.2: Improved pit stop exit prediction when opponents are in the pitlane; Don't reset the fuel data when leaving the pits during a race, unless we've added fuel.

Version 4.9.3.1: Hotfix for pitstop prediction additions (see previous release notes). This fixes the wildly inaccurate pit stop time loss estimates reported by the pitstop benchmark process if you make the benchmark request too late in the lap (after you've entered the final sector)

Version 4.9.3.0: First cut of pit exit prediction - obtain a benchmark pit time in practice by making the 'time this stop' / 'practice pitstop' / 'time this pitstop' / 'pitstop benchmark' command or pressing the assigned "Pitstop prediction" button before your practice stop. The pit exit prediction will play automatically some time after requsting a stop, or if you make the 'Where will I be after a stop?' / 'Estimate pit exit positions' / 'What will happen if I pit?' command or press the "Pitstop prediction" button during a race (see 'Pit exit position prediction' section above); Disable cold brakes warnings for some car classes; R3E - detect if this and next laps are invalidated due to cut track in quali/practice; R3E - disable pit stall position messages in post race; R3E - improve cut track detection; R3E -  fix best lap time not announced during leaderboard challenge; AC - fix crashes due to unexpected timings reported by the game; Suppress crashes when using Naudio Input - (but please send us log file if you get exceptions in log); Fix issue where best lap time is lost if it is set after an invalid lap; Fix incorrect gaps announced if vehicles aren't within the same lap; Fix DRS messages announced incorrectly if opponent is pitting; Make 'half fuel' call play at exactly half fuel (was playing a bit too early); App wide bug fixes

Version 4.9.2.9: Allow pit countdown past session finish (for cooldown lap); Don't grumble about a stopped engine if the car is still moving; some additional checks to handle delayed opponent position data; iRacing - improve live position tracking; iRacing - improve retired vehicle detection; iRacing - race finish position improvements; iRacing - finishing position detection improvements; iRacing - reliability fixes; rF2 - suppress pit messages while in garage/monitor; AC - fixed crash bug on 2-sector tracks; R3E & pCars2 - use game-provided time gaps to car in front / behind (more accurate than derived gaps); R3E - DRS message accuracy improvements; R3E - removed auto-confirm pit macro and associated logic (no longer needed, and can cause some issues with pitstops); R3E - disable spotter in hotlap mode

Version 4.9.2.5: Added commands to check self pace, you can ask "how's my self gap" / "how's my personal pace" / "how's my delta best" to see how you're doing relative to your own best lap; iRacing - fixed pit stall countdown; iRacing - fix finish position reporting; iRacing - allow pit voice commands to be enabled or disabled ("Enable iRacing pit stop commands", default enabled). Disabling them removes them from the phrases the speech recognition engine listens for, potentially improving accuracy for other phrases as well as preventing false positives; added ability to specify if car class has speed limiter via "limiterAvailable" class field; rF2 - enable pit stall countdown (doesn't work on some tracks, disabled on ovals); rF2 - add option to force Rolling Start start type.

Version 4.9.2.4: Fixed incorrect car class data in R3E online practice and qualifying sessions; logging readability improvements; small AMS performance improvement; some code cleanup

Version 4.9.2.3: Hotfix for Assetto Corsa broken spotter

Version 4.9.2.2: AMS off-track warning tweak - make 'kerb' a legal surface to drive on (should make the warnings much less common); A few other AMS fixes; Half distance fuel calculation tweak to make it a little more conservative; iRacing workarounds for missing opponent data when cars aren't rendered by the game; A few other iRacing bug fixes; Some UI tweaks to fix tab ordering and add shortcut keys (underlined on the UI)

Version 4.9.2.1: Fixed missing fuel data in RF2; Added option to specify how much 'reserve' to add when doing fuel calculations (enable the "Enable tight fuel calculations" property and set the reserve amount with the "Additional fuel to finish the race" property); Fixed iRacing SoF calculation; Various minor tweaks

Version 4.9.2.0: New RF2 shared memory plugin with better performance; More work to fix iRacing crashes since switching to the faster parser; Reinstated an iRacing crash fix that got lost in a previous update; Added an option to fall back to a slower, safer session info parser method in iRacing (property 'iRacing faster parser'). If the app crashes for you, uncheck this option to use the slower parse method and please post your findings a debug log in our forums so we can track the issue down

Version 4.9.1.7: Added tyre temperature thresholds for some missing tyre types (including wets) - should fix incorrect temperature warnings on wets and some other tyres; Mapped player tyre type in pCars 2 (note this doesn't support mixed tyre types); Fixed an issue with iRacing driver swaps that caused log-spam and performance issues

Version 4.9.1.6: iRacing hotfix for crash in mapper

Version 4.9.1.5: More detailed error messages; pCars and pCars2 performance improvements; iRacing performance improvements; AC fixes for shorter tracks (2 sectors); Fixed inaccurate race position messages in iRacing 'checkered flag' phase; Some iRacing stability improvements; Use relative lap time difference when working out when to give multi-class warnings. This uses 2 new Properties - 'Multi-class slower car warning time' and 'Multi-class faster car warning time' - this is the approximate average amount of notice you want the app to give you when approaching a different class car; Added iRacing voice commands to get out of car ('get out') and get the average SoF ('what's the sof' / 'what is the strength of field'); For Assetto Corsa, only allow multi-class races if the number of unknown car models does not exceed property 'Max unknown car models in ACS multi-class races' (default 0) - this is necessary because Assetto doesn't expose proper class data. This prevents the app dividing participants into different classes unless the app recognises all the car models in the session

Version 4.9.1.4: Workaround for iRacing 'opponent pitting now' message spam

Version 4.9.1.3: Minor multi-class tweaks; Assume single class if all car classes in a race are unknown to the app (AC, rF2 and rF1 only); Added button to save console output to a file (in Documents/ CrewChiefV4/debug/); Some AC internal fixes; Don't give damage and tyre wear responses in iRacing; Added Lee spotter (in latest sound pack update)

Version 4.9.1.1: Major internal overhaul to fully support multiclass races - opponents and positions are now tracked correctly for the player's car class, the app doesn't make calls about opponent laptimes, pitting, and other stuff if they're not in the same class as the player; added multi-class specific messages for when you're lapping or being lapped by other classes; added multi-class specific voice commands "is the car ahead in my class", "is the car behind my class", "what class is the car ahead", :"what class is the car behind" and variations; more performance improvements; speculative fixes for driver swaps in iRacing; fixed 'enable driver names' flag being ignored for some messages; added optional pit box countdown (R3E and iRacing only, enable with property "pit_box_position_countdown"); rF2 improvements: improved rolling start detection and distance to safety car detection, in SCR mode Crew Chief now respects DoubleFileType value and will announce last FCY lap Frozen Order instructions correctly; added option to change left/right side tyres(iRacing only); modified R3E pit macros to take advantage of modify pit menu behaviour (should make the pit macros, particularly the auto-confirm pit actions, more reliable); retrieve gap ahead / gap behind as late as possible when playing gap messages to improve accuracy; made auto-refuel ("fuel to the end" macro in R3E and "pitstop fuel to the end" command in iRacing) a bit more conservative - more so at longer tracks

Version 4.9.0.7: Added messages for oil and fuel pressure warnings (iRacing only); Added messages for stalling the engine; Added messages when you crash very heavily - the app will ask if you're OK and, if you're using voice commands, wait for you to respond (this can be disabled with the "Enable crash messages" option in Properties); Only use start-line track temperature in pCARS to prevent the app calling local track temp variations; Allow multi-class support to be disabled ("Force single class" property) - useful if you're playing with 3rd party content that has incorrect car class IDs; More performance improvements; Prevent the app's windows being resized such that controls are no longer visible (this behaviour can be disabled by unchecking the "Force minimum window size" property)

Version 4.9.0.6: Don't play 'good pass' message in pCARS 2 if we've just collided with someone (might still need some tuning); some performance improvements; fixed issues with historical weather data being lost; pCARS 2 pit window fixes (again...); fixed missing DQ / DNF session end messages; fixed mid-point fuel report saying "fuel looks good" when it clearly isn't good

Version 4.9.0.5: Fixed 'box this lap' messages playing in pCARS2 even though the player had completed his mandatory stop; some pCARS2 off track warning tweaks; Reworked R3E opponent lap invalidation code - should prevent the app incorrectly invalidating opponent laps; speculative fix for session end being 1 lap too late for iRacing fixed number of lap races

Version 4.9.0.4: Added voice command to get and estimate of how much fuel is needed to finish the race - "how much fuel to the end" / "how much fuel do we need"; Added voice command to get an estimate of how long the tyres will last (in minutes for timed sessions, laps for fixed lap number sessions) - "how long will the tires last" / "how long on these tires" / "how long will these tires last"; Added check to prevent a stopped car triggering incident warnings repeatedly; Added ability to set pit fuel amount in iRacing - "pitstop fuel to the end" / "pitstop fuel to the end of the race" - and via a macro in R3E and pCARS2 - "fuel to the end" / "fuel to the end of the race". See the 'Advanced command macros' section of the help.txt file or https://thecrewchief.org/showthread.php?263-Command-key-press-macros&p=2378&viewfull=1#post2378 for more info (please read the documentation before using this in R3E or pCARS2 as it requires the default in-game pit strategy to be configured in a particular way, and is quite fragile - use at your own risk); Immediately close the pit menu in R3E after running a macro if we're close to the pit entry (if the menu is left open when you cross the limiter line, the auto-confirm macro will fail); If sound pack downloads fail, retry with the other server.

Version 4.9.0.3: Don't make Assetto Corsa spotter calls when viewing replays; reworked some of the sector gap logic; derive opponent laptimes and sector 3 times in Raceroom instead of using data provided by the game as the data provided by the game are always 1 lap out of date; reduce frequency of repeated "the next car is..." messages

Version 4.9.0.2: Fixed some issues in pCARS 2 session end detection and pre-start message triggering; disable pcars2 spotter in pits; some minor performance improvements

Version 4.9.0.1: Hotfix to prevent some stock car rules messages triggering when they shouldn't; don't read lap times or gaps if we're under full course yellow; added possible track cut warning for pCars2

Version 4.9.0.0: Added support for rF2 StockCarRules plugin, CC will now announce Lucky Dog, Wave Around, EOLL messages. To enable make sure you enable the "Use American terms" option, disable StockCarRules plugin in rF2, and set "EnableStockCarRulesPlugin":1 for "rFactor2SharedMemoryMapPlugin64.dll" - see the "rFactor2 Stock Car Rules (SCR) plugin" section above or  https://thecrewchief.org/showthread.php?407-How-to-enable-rF2-Stock-Car-Rules-in-Crew-Chief&p=2931&viewfull=1#post2931 for more details; Add option to disable pit state announcement during FCY in rF2 and rF1/AMS; Disable brake temp messages on ovals; fixed pit macros not working for some R3E players; prevent some messages playing when they're no longer relevant; some internal fixes

Version 4.8.3.2: Fixed AC plugin after game update - the app should ask if you want to update the plugin when you first launch it in AC mode; More fixes to the manual rolling start logic; iRacing session transition crash fix; some car class tweaks; added nAudio speech recognition code to allow voice recognition input device selection (enable with property "Use nAudio for speech recognition" - thanks to Daniel Nowak for this one); disable sector delta messages on ovals and use more generous spotter parameters

Version 4.8.3.1: Corrected some pCARS 2 track names that got changed in the last pCARS 2 patch; disable some irrelvant sounds when racing on ovals; work around for some missing spotter sounds; a few internal fixes

Version 4.8.3.0: Changed personalisations download process to reduce bandwidth use

Version 4.8.2.9: Fixed a serious regression in multi-class race position tracking

Version 4.8.2.8: Added more variety to race finish messages; make default R3E pit macro pause a while before closing menu; warn when an opponent car is exiting the pits; a few other minor bits and bobs

Version 4.8.2.7: Experimental support for double-file manual rolling starts (R3E, pCARS2 & AC only - enable with "Manual formation lap double-file start" property); fixed arrow keys and some other keys not being released when used in command macros; iRacing rally cross fixes; more Formula E battery tracking logic and messages; split some longer voice command responses so if you want to hear more, you have to ask ("more information" / "more info" / "clarify") - currently only implemented for Formula E battery messages, but will be extended. If you want all the information in a single long response without having to ask for clarification, enable "Verbose messages" property; added missing RF1 / AMS blue flag override ("Enable AMS / rF1 blue on slower" property); some internal fixes

Version 4.8.2.6: Fixed some nAudio bugs that meant the radio beeps were being sent to the wrong audio device; added Hong Kong track mappings (RF2 Formula E pack); fixed tyre temperature thresholds on some R3E car classes; added voice command to get player incident count ("how many incidents do I have" / "what's my incident count") and session incident limit ("what's the incident limit") - iRacing only; added voice command to get player licence ("what's my licence class") and iRating ("whats my iRating") - iRacing only; added voice command to get opponent licence ("what's [the guy in front's / the leader's / p10's / Bob's] licence class") and iRating ("what's [the guy in front's / the leader's / p10's / Bob's] iRating") - iRacing only; battery monitoring bug fixes

Version 4.8.2.5: Reworked battery status message (response to voice command/button command), added battery use increase/decrease detection; added pit stop related messages for pCARS2; some internal bug fixing; allow spotter sounds ("car left" etc) to have their volume scaled relative to the other voice messages (property "spotter_volume_boost")

Version 4.8.2.4: Added battery related logic for electric cars (RF2 Formula E)

Version 4.8.2.3: Fixed some performance issues when using nAudio playback; Some internal sound player rework and bug fixing; Added "TTS volume boost" - by default the TTS sounds are now played at 2x volume, which balances them better with the the other sounds; Added TTS trim start and trim end properties to remove silence from TTS sounds; Added "Only use TTS when there is no alternative" option. When set to 'true' (the default) the app will drop messages or use generic terms to refer to opponents when it doesn't have a driver name recording. It will use TTS for driver names only if the message is considered essential (when e.g. responding to "who's in front?" voice command). The recommended TTS configuration is now to enable TTS, set "Only use TTS when there is no alternative" to true, set "TTS volume boost" to 2, "Trim end of TTS sounds" to 600 and "Trim start of TTS sounds" to 100 - see 'Help and getting started' for more information; Allow iracing tyre pressure adjustment voice commands to be made in PSI (when property "iRacing tyre pressure adjustments in PSI" is true the app assumes you mean PSI); Fixed broken voice recognition for "who's ahead", "who's behind" and "who's leading" commands; Added Ross spotter to latest sound pack; Allow iRacing fuel to be added in gallons

Version 4.8.2.1: Work-around for R3E missing sector number updates when cars exit to pits (should fix a lot of inaccurate calls in practice and qualifying); fixed mute not working properly in nAudio mode; Simplified track landmark generation (see Help); Some internal bugfixes

Version 4.8.1.9: Improvements to speech recogntion accuracy; integrated nAudio library for sound playback which allows you to choose play back devices for messages and background sounds (disabled by default - enable this with the "Use nAudio for playback" option) - this also allows the app to play back at higher volumes; added lots of iRacing pit commands (see the voice commands section in Help); lots of iRacing bug fixes; added a new spotter (Micha - in the latest sound pack)

Version 4.8.1.7: Opponent to player delta bug fixes (all games); added macros for some iRacing black-box interactions; lots of iRacing fixes; added new Pace Notes feature for recording and playing back user-created pace notes (see Help); fixed arrow keys not working for macros; fixed a few minor lap time reporting issues

Version 4.8.1.5: Better UI handling of sound pack downloads (app should no longer appear to hang while unpacking sounds); Announce retired and DQ'ed drivers in RF2; Fixed stale driverID in iRacing; Minor bug fixes - Mark out laps as invalid in R3E (should prevent stale lap time calls at the end of an out lap), RF2 pre-lights and overtake messages and track landmark mapping getting lost on session restart, pCARS 2 pit exit messages;

Version 4.8.1.4: Better pruning of pCARS2 stale and duplicated opponent data in online sessions (hacks adapted from pCARS1 to work around bugs inherited from pCARS1); Added opponent retired and opponent disqualified messages (pCARS2 / R3E); Some bugfixes in R3E for invalid lap handling; R3E and pCARS2 car and track mapping fixes

Version 4.8.1.3: Fixed an issue in the speech recogniser where grammars were initialised multiple times, reducing recognition accuracy - this should make the recogniser work more reliably; Fixed an issue in R3E and AC where a lap invalidated in sector 3 could cause stale best-lap data to be announced; Remove opponents who are reported as DNF / DQ / DNS in R3E; Added experimental option 'Disable alternative voice commands' (disabled by default). Enabling this will force the speech recogniser to only load the first command from each row in speech_recognition_config.txt. Instead of recognising any of "who's leading", "who's in the lead", "who is leading", "who is in the lead", or "who's the leader" the app will only recognise the first in the list - "who's leading". This will limit the number of phrases the recogniser understands and *may* improve recognition accuracy

Version 4.8.1.2: More iRacing bug fixes, mostly around session state tracking; Improved default pCARS2 command macros (massive thanks to Belaki on the pCARS forums); fixed pCARS2 pit window end message playing when it shouldn't; Added more tyre wear messages; RF2 bug fixes; Added overall damage report - triggered by voice command ("damage report / how's my car / is my car ok") or button assignment;Added session status report (race time remaining, pit status, gaps etc) - triggered by voice command ("session status / race status") or button assignment;Added car status report (damage, fuel, tyres etc) - triggered by voice command ("car status") or button assignment;Added combined status status report (session status + car status report) - triggered by voice command ("full update / full status / update me") or button assignment; Added a few more messages

Version 4.8.1.1: Corrected and added some corner mappings; Added function to read corner mappings for the current lap (activated with a button assignment or voice command "read corner names" / "corner names" / "tell me the corner names"); More iRacing beta updates; Tweaked overtake message probability

Version 4.8.1.0: iRacing beta; PCars2 beta (shared memory only - UDP isn't ready); Pit command macros beta (example implementations for R3E and pCARS2 included - see the "Command macros" section at the end of the Help file, and saved_command_macros.json in the app's installation folder); fixed lap time issues in AC (caused by the laptime not being sent to the app at the same time as the new-lap notification); extended support for RF2 StockCar rules plugin; more car class and track mapping data; RF2 opponent pit detection and additional pit messages; Mute the background sound when you talk to the chief; Block all messages when you're talking to the chief (optional - use property "Block messages when talking to the Chief"); lots and lots of bugfixes for RF2, PCars, AC and R3E; Attempt to predict when it might rain for PCars and PCars2 (enabled by default, use property "Enable PCars rain prediction using CloudBrightness" to disable). Note this uses changes in reported CloudBrightness value and is quite inaccurate

Version 4.8.0.7: RF2 fixes - fixed low fuel message playing when it shouldn't, fixed European versions of full course yellow messages not playing on new installs

Version 4.8.0.6: RF2 DRS support; RF2 full-course-yellow, standing start and rolling start order messages; R3E opponent laptime validation fix; don't play aero damage if the rest of the car is knackered; some AC fixes; Fixed first lap out of pits massively skewing fuel consumption estimate

Version 4.8.0.3: RF2 session transition fixes

Version 4.8.0.2: Fixed broken PCars2 oval track mapping - should now correctly call 'car low' / 'car high' on ovals for all spotters except Geoffrey

Version 4.8.0.1: Added some Project Cars 2 stuff - game type, launch options, and a couple of initial track mappings; fixed some incorrect tyre temperature warnings

Version 4.8.0.0: Updated Assetto Corsa plugin to be compatible with latest AC update; Added "Block messages when talking to the chief" option - this prevents any messages being played while you're making a voice command; Some fuel consumption calculation fixes.

Version 4.7.9.9: R3E 64Bit support; Added voice command to tell you (once the data is availalbe) what the relative performance difference is between different tyre types based on the best lap in the session for tyre type - e.g. "Softs are about 0.4 seconds faster than Mediums, Mediums are about 1.2 seconds faster than Hards". The voice command is "Give me tyre pace differences", "What are the tire speeds?" / "Whats the difference between tires?" / "Compare tire compounds" (R3E and RF2 only); Added opponent tyre type info when they leave the pits after changing tyre type (R3E and RF2 only).

Version 4.7.9.7: More RF2 race end detection fixes; Added Imola corner mappings for R3E; fixed race start message being repeated on manual rolling starts; some additional checks to prevent mandatory pitstop messages playing during or after you've completed your stop.

Version 4.7.9.5: Compatibility fixes for new Raceroom shared memory layout; Map to new Raceroom tyre types - you can ask "what tyres is [opponent name] on?", or "what tyres am I on?"

Version 4.7.9.4: Better sound pack update mechanism - wastes less bandwidth; Added "minimise to tray" and "start minimised" options. Minimise to tray places a Crew Chief icon in the system-tray when you minimise the app, with a right-click menu contain commonly used functions. The app can be started minimised if desired; Only allow manual formation lap mode to supress messages in race sessions (fixes missing messages in practice & qual with manual formation lap mode enabled); Fixed session end detection in RF2 / AMS / RF1 - the app should also detect when you click 'next session' without clicking 'end session' first; Added a new fuel calculation command ("calculate fuel for [x] [laps / minutes / hours]" / "how much fuel do i need for [x] [laps / minutes / hours]" / "how much fuel for [x] [laps / minutes / hours]" / ) - for example, "how much fuel do I need for twenty minutes?". Assuming the app's had the chance to record your fuel usage over at least one lap (the more the better), it'll estimate how much fuel you'll need for the requested number of laps / time based on your average consumption. IMPORTANT: if you use this to set your fuel load in a timed race you MUST add extra fuel to account for the lap you need to finish after the race timer reaches zero. The app WILL NOT DO THIS FOR YOU - if you put in exactly 20 minutes worth of fuel for a 20 minute race you'll run out on the last lap; Fixed some issues in the fuel use tracking logic.

Version 4.7.9.0: Fixed initialisation errors in TTS engine which prevented the app from starting when "Use TTS for missing names" was enabled; added option to select where the race starts on a manual formation lap - "Manual formation 'go' when leader crosses line". If this is true (the default) the app assumes that the race starts and cars are allowed to overtake as soon as the leader crosses the start line. If it's false, the app assumes that no one is allowed to overtake until they cross the start line; Added some RF1 session identifier fixes - should correct a few issues caused by the app thinking the race was actually qualifying.

Version 4.7.8.9: Fix for some settings getting corrupted on system which use a comma as a decimal separator - this causes some spotter and voice recognition issues. If you have already encountered this, please reload the app's default settings; Some fuel use calculation fixes; Spotter fixes; Added manual formation lap support. This supresses most messages on lap 1 and plays an alternate sequence of start messages - it assumes you're not allow to pass until you cross the start line. This mode can be activated and deactivated with a button press ("Toggle manual formation lap mode") or a voice command (enable with "this is the formation lap" / "formation lap" / "rolling start", disable with "standing start" / "no formation lap").

Version 4.7.8.6: Added optional radio beeps for when the spotter or the Chief interrupt each other. "Insert beep-out between Spotter and Chief" plays the close-channel beep after the chief / spotter has finished, and "Insert beep-in between Spotter and Chief" (the default) plays the open-channel beep before the chief / spotter interrupts. The spotter and chief use different beep sounds here, and these options can be combined if you want 2 beeps (close then open) when interrupting; Attempt to delete corrupted settings and force the app to restart if they can't be processed; Don't play fuel consumption estimate if it's 0 litres per lap

Version 4.7.8.1: RF2 plugin fixes for car damage issues in online races
	
Version 4.7.8.0: Fixed spotter logic where it would consider 2 cars along side to be "3 side", even if those cars were one behind the other; Use oval spotter messages (inside / outside) when on known oval tracks, if the selected spotter has these sounds; Tweaked spotter enable / disable sound to be a bit more appropriate for non-default spotter voice packs; Fixed broken sector 3 time deltas in Project Cars

Version 4.7.7.9: Fixed Assetto Corsa pit window open calculation for sessions with a fixed number of laps; Fixed a crash bug when starting the app with no sound pack
	
Version 4.7.7.8: Added dropdown to main screen to allow a different spotter voice to be selected; Added Geoffrey Lessel's awesome spotter sounds - these are in the latest sound pack. Select "Geoffrey" from the new 'Spotter voice pack' menu; Fixed a couple more spotter bugs; Added button binding to get fuel status (consumption and fuel remaining). The "how's my fuel?" voice command now reports the consumption as well as the remaining fuel

Version 4.7.7.5: More Scoops-Brand RF2 corner mappings; Fixed some spotter bugs; Added searching to Properties screen to make it a little less user-hostile; Replaced the nasty underscore_property_names with proper names on the Properties screen

Version 4.7.7.4: Substantial RF2 plugin rewrite; Attempt to map game data if we detect the PCars2 exe - this does (apparently) work but expect some bugs and issues; Ported some fixes from the PCars Android app - some free practice session improvements, more aggressive pruning of broken driver data, better (hopefully...) method of identifying player's data so monitoring other drivers shouldn't confuse the app.

Version 4.6.7.2: Use game-provided mandatory pitstop data where available (should fix app thinking you've completed your mandatory pitstop when the game thinks otherwise); Some more car and track mappings from Scoops; Added 'three wide you're on the left' and 'three wide you're on the right' to the spotter - optional, disabled by default (spotter_enable_three_wide_left_and_right in the Properties screen); Added voice command to get info about the car in front / behind is slower / faster than you - "where should I attack" / "where am I faster" / "where can I attack" or "where should I defend" / "where am I slower" / "where is he faster" / "where will he attack"; A few bug fixes; mapped Watkins Glen for PCars; Added fuel use per lap response - "what's my fuel usage" / "what's my fuel consumption" / "what's my fuel use".

Version 4.6.6.4: Overhauled internal sound handling to make the app behave better; Faster start up times; Better CPU usage; some internal fixes; more of Scoop's corner mappings (recordings still to be done)

Version 4.6.6.2: Fixed some RFactor and RFactor2 issue; Added a few more track location mappings (thanks again Scoops); Disabled 'incident ahead' in R3E while I resolve the false-positives

Version 4.6.6.1: Added a big set of corner name and location data thanks to Scoops' hard work; Overhauled yellow flag logic for sector yellows and local yellows; Read times accurate to hundredths of a second in some circumstances; added oval-specific behaviours (enabled per-track with a flag in trackLandmarks.json) - ignores brake and left side tyre temps, estimates tyre wear from right side tyres only; added an experimental 'realistic mode' option - enabling this supresses some messages based on car class and track (e.g. spotter is off at start of session when not on ovals, older car classes have less telemetry based info like tyre temps) - this is very much 'work in progress'; added per-car class behaviours for yellow flag phrasing (e.g. pace car vs safety car) and last lap message (e.g. "white flag" for last lap only applies to Indy and NASCAR cars); Added 'force update check' button.

Version 4.6.5.2: Fixed AC plugin after 1.14 update.

Version 4.6.5.1: Fixed a silly bug in the legacy R3E blue flag detection (used when flag rules are disabled).

Version 4.6.5.0: Automatically switch between yellow and blue flag implementations in R3E depending on whether flag rules are enabled in-game; Added some more incident calling and yellow flag options; Added button mapping and voice command to suspend and enable yellow flag messages - enable with "give me yellows", "tell me yellows", "give me incident updates" or "give me yellow flag updates". Disable with "no more yellows", "stop incident updates", "don't give me yellows" or "don't tell me yellows"; Fixed the "where is Bob?" voice message response in qual and practice sessions

Version 4.6.4.9: Added R3E Mantorp Park (long), Norisring and Sachsenring corner data; A bit more R3E flag tweaking

Version 4.6.4.8: Skip 'dead' opponent data copies coming from PCars (should fix a few issues with inaccurate opponent data); Added R3E Hungaroring corner data; Made opponent incident detection less sensitive and added an option to disable it (enable_simple_incident_detection); Reworked opponent lap and sector handling to fix incorrect sector time and pace reports (all games); Tweaked R3E yellow flag reporting to allow status changes to settle before reporting
	
Version 4.6.4.7: A few minor bug fixes

Version 4.6.4.6: Some more Raceroom flag calling tweaks - should be less noisy but may miss rapidly changing flag situations; Added RF2 'stock car' mode (sound recordings to follow);Added R3E Nurburgring GP corner data

Version 4.6.4.5: Some Raceroom flag calling tweaks

Version 4.6.4.4: Initial support for Raceroom yellow flag implementation and revised shared memory layout; a few minor bug fixes

Version 4.6.4.2: Delay lead change messages slightly and validate before playing; fixed potential error when working out where a pileup has occurred 

Version 4.6.4.1: Fix crash bug in PCars with UDP data

Version 4.6.4.0: Added installer for game specific plugins (AC, AMS / RF1 and RF2) - the app now offers to copy the required plugin files to the games' install directory if they're missing or out of date; extended incident reporting logic and sounds to allow for multiple involved drivers to be reported (if we have the driver name sounds) or 'pileup' warning if 4 or more drivers are stopped in the same corner; added more corner landmarks; opponent tracking fixes for PCars; loads and loads of bugfixes and improvements

Version 4.6.3.2: Temporary hack to reduce wheel locking sensitivity - hopefully will prevent false-positives while I work out a better algorithm

Version 4.6.3.1: Temporary hack to reduce wheel spin sensitivity - hopefully will prevent false-positives while I work out a better algorithm; fixed Assetto Corsa pit window calls being 1 lap out

Version 4.6.3.0: New Python module for Assetto Corsa - please replace your existing ...\Steam\steamapps\common\assettocorsa\apps\python\CrewChiefEx\ folder with the new one in the app's install folder; New plugin for RF2 - please replace your existing ...\Steam\steamapps\common\rFactor2\Bin64\Plugins\rFactor2SharedMemoryMapPlugin64.dll with the new one in the app's install folder; Added corner names to some calls on several tracks (this is will a work-in-progress); Revised RF2 and AMS opponent data handling to fix missing gap messages; More work on AMS session end bugs; Added better reporting of yellow flags for AMS and RF2 - the app will sometimes tell you who's involved in the incident and what corner the incident is in; Added simple incident reporting for known corners in R3E, AC and PCars; Added attack / defend calls for known corners (note these messages don't play often - this is intentional); Added brake locking and wheel spin reporting for known corners; RF2 timing accuracy improvements; Added Assetto Corsa damage reporting; Lots of minor bugfixes and improvements

Version 4.6.1.5: Fixed AMS multi-class support and added some AMS car classes; Corrected AMS session end logic - should prevent session end messages playing until you complete your lap; Reworked AMS opponent lap time handlingdon't play 'chequered flag' message in race sessions; fixed messages not playing in unlimited timed sessions; some bug fixes

Version 4.6.1.4: Fixed PCars timed sessions

Version 4.6.1.3: Fixed broken personalisations (oops)

Version 4.6.1.2: Fixed broken settings preventing changes to any setting from being saved

Version 4.6.1.1: Integrated personalisations - the app will ask you to download a new "Personalisations" sound pack. When this is complete the "My name" drop down box (top right) has a long list of names the app can use when addressing you. This replaces the old method of manually unpacking a prefixes_and_suffixes folder to the app's sounds; Work-around for Assetto Corsa sometimes giving out of date position information; Disable multi-class code for RF1 based games because the vehicle type data from Automobilista is too vague (things like "Ford" and "Peugeot"); Ported RF2 full course yellow and sector-specific yellow flag announcements to Automobilista; Removed irrelvant pit window messages from RF1 based sims - in offline sessions, if a pit schedule is defined the app will call "box now" in accordance with this schedule (assuming equal stint lengths) - this can be disabled with enable_ams_pit_schedule_messages property;Fixed cut track warnings playing on out laps in Automobilista

Version 4.6.0.5: Major overhaul of time reading (English sound pack - users of the Italian sound pack are unaffected); added RF2 caution period and yellow flag events; scan for controllers only on request (press the "Scan for controllers" button to update the app's list of controllers) - this also improves the app's startup time; fixed Assetto Corsa missing race start after 1.12 patch; added chequered flag message for timed sessions (still some issues here with PCars); reworked PCars session end detection; added controller bindings for message volume up / down; added some simple help text (much much more needs to be added to this); externalised car class definitions (first version - lots more work to do here); lots of bug fixes

Version 4.5.0.0: First cut of RF2 support, thanks to The Iron Wolf. This needs an additional .dll plugin for RF2 - see https://forum.studio-397.com/index.php?threads/crew-chief-v4-5-with-rfactor-2-support.54421/ Updated some Raceroom car classes

Version 4.4.3.4: Some controller cleanup tweaks

Version 4.4.3.3: R3E patch update

Version 4.4.2.4: Fixed controllers initialisation bug which should fix very slow (2-3 minutes) startup time for some users - thanks Tako.

Version 4.4.2.3: Removed some debug calls

Version 4.4.2.2: Only cancel pre-lights messages on throttle application; Added option to disable yellow flags in Assetto Corsa, and made them a little less frequent; Some Assetto Corsa opponent position fixes

Version 4.4.2.1: Fixed some issues with pre-lights messages

Version 4.4.2.0: Reworked pre-lights message logic (optional) - app will play race session messages while you're on the grid until the throttle / brake / clutch is pressed, then it'll play the 'get ready' message. This can be enabled by selecting 'play_pre_lights_messages_until_cancelled' option on the Properties screen; Some driver name mapping fixes.

Version 4.4.1.3: Added more tyres for Assetto Corsa; fixed missing 'standby' response delay; reduced pre-lights message queue length; some Italian translation support fixes.

Version 4.4.1.2: "How are my tyre temps" and "How are my brake temps" now give the status (hold / good / cold) rather than the actual temps.

Version 4.4.1.1: Fixed AC spotter being disabled at the start of each lap; Fixed crash when selecting AC as the game type if the previous game type was AMS; Started wiring up AC tyre wear / temp data (just GT3 class so far).

Version 4.4.1.0: Added missing AMS / RF1 / GSC command line parameter game selection; Final version of Assetto Corsa Python module. IMPORTANT: remember to update the CrewChiefEx Python app from this new release (copy the CrewChiefEx folder from the app's install location to .../Steam/steamapps/common/assettocorsa/apps/python/).

Version 4.4.0.5: More Assetto Corsa additions. IMPORTANT: remember to update the CrewChiefEx Python app from this new release (copy the CrewChiefEx folder from the app's install location to .../Steam/steamapps/common/assettocorsa/apps/python/).

Version 4.4.0.4: More Assetto Corsa additions. IMPORTANT: remember to update the CrewChiefEx Python app from this new release (copy the CrewChiefEx folder from the app's install location to .../Steam/steamapps/common/assettocorsa/apps/python/).

Version 4.4.0.3: More Assetto Corsa additions (no changes to the CrewChiefEx python app in this revision).

Version 4.4.0.2: More Assetto Corsa additions and fixes including some performance improvements. IMPORTANT: remember to update the CrewChiefEx Python app from this new release (copy the CrewChiefEx folder from the app's install location to .../Steam/steamapps/common/assettocorsa/apps/python/). Note that sector times in multi-player are not yet accurate, and that player needs to drive 1 lap in single-player before sector gaps have been recorded.

Version 4.4.0.1: Added missing Python plugin for Assetto Corsa. Copy the CrewChiefEx folder from Crew Chief's installation location to /Steam/steamapps/common/assettocorsa/apps/python and activate the plugin in-game.

Version 4.4.0.0: First cut of Assetto Corsa support courtesy of Sparten - this is a work-in-progress. Copy the CrewChiefEx folder to /Steam/steamapps/common/assettocorsa/apps/python and activate the plugin in-game; added blue flag max trigger distance (increase this to make the blue flag warnings play when the lapping car is further away). 

Version 4.3.0.4: Fixed incorrect sector gap reports for rF1; Fixed session variables not resetting at start of new session for rF1; Disabled erroneous damage reporting in Hot Lap sessions for rF1; Fixed erroneous fuel warning messages in non-race sessions for rF1; Fixed erroneous flags in non-race sessions for rF1; Added basic invalid lap detection for rF1; Improved wheel spin/lock detection for rF1;

Version 4.3.0.3: Fixed 'leader is pitting' message for rF1; Improved opponent state tracking for rF1 (allows for duplicate AI in grids); Adjusted scheduled pit stop notifications to be offline/single player only for rF1; Adjusted 'pit now' message for scheduled stops to play before passing pit entrance for rF1; Fixed 'green green green' messages after formation lap for rF1; Disabled spotter during formation lap for rF1; Added 'get ready' message during final sector of formation lap for rF1; Fixed incorrect brake temperatures for rF1; Improved multi-class race support for rF1; Added penalty notifications; Fixed 'the next guy is' message spamming for rF1; Fixed 'the gap behind is reeling you in' message for rF1

Version 4.3.0.2: Fixed opponent lap timing and sector gap reporting for rF1; Fixed blue flag behavior for rF1; Adjusted damage reporting for rF1; Fix session type and session phase detection for rF1; Improve pit window mapping for rF1; Add green flag and off-track detection for rF1; Scheduled pit stop detection for rF1;

Version 4.3.0.1: Fixed tire temp warnings for rF1; Fixed pit exit traffic notifications for rF1; Added black flag notification for rF1; Adjusted blue flag behavior for rF1; Adjusted invalid lap detection for rF1; Added ambient temps, track temps, and wind info for rF1; Added detached wheel info for rF1; Fixed auto-launch for rF1; Added separate menu items for Automobilista, Stock Car Extreme, Copa Petrobras de Marcas and Formula Truck; Adjusted auto-launch options for R3E.

Version 4.3.0.0: Initial (beta) support for rFactor 1/Automobilista/Stock Car Extreme. Download 'rFactorSharedMemoryMap.dll' from https://github.com/dallongo/rFactorSharedMemoryMap/releases/latest and place it in the sim's Plugin folder, then select 'rFactor' in Crew Chief.

Version 4.2.1.8: Include more laps in the opponent vs player laptime comparisons during race sessions

Version 4.2.1.7: Use lastSectorTime data for opponent cars when in PCars UDP (network data) mode. This makes the opponent lap time reports accurate as the app doesn't have to time them itself (this data isn't available in PCars shared memory data)

Version 4.2.1.6: Fixed PCars practice and qual session data being cleared when pitting (should fix a lot of the inaccuracies in these sessions); Pause messages after a "stand by" response

Version 4.2.1.5: Fixed Raceroom WTCC 2014 tyre heating thresholds

Version 4.2.1.4: Fixed Raceroom BMW M1 tyre heating thresholds;A few internal tweaks and fixes

Version 4.2.1.3: Don't repeat "stand by" or "didn't understand" messages when responding to a "repeat please" voice command; Fixed 'what time is is' voice command (thanks Gongo)

Version 4.2.1.2: Added more logging around UDP packet reception and processing; Fixed a couple of memory leaks; Don't play 'no tyre wear' after changing tyres

Version 4.2.1.1: Fixed a bug in the gap-ahead logic that was triggering 'keep him under pressure' messages too often

Version 4.2.1.0: Added support for secondary driver names mappings file 'additional_names.txt' so the auto-updater doesn't overwrite user-made changes to names.txt; Additional validation on R3E sector reports; Added "what's the fastest lap" and "what time is it" voice commands (reports session best lap for player class, and current [real world] time of day); A few bug fixes and minor improvements; Reworked R3E tyre temperature checking to make better use of the core temps provided by the game (for new physics model cars).
	
Version 4.2.0.1: Added ADAC 2015 and F4 RaceRoom class; PCars suspension damage threshold tweak; Damage reporting rework; Various bug fixes and minor improvements; Don't play fuel messages while being refuelled; Don't play wheel spin / locking when in the pits or when we have a puncture or missing wheel; Fixed best lap and brake damage voice commands; Added brake and tyre temp warning on pit exit (when temps aren't optimal) - these are optional (brake temp warning is on by default, tyre temp warning is off); Some voice commands now trigger a "stand by" response, then a few seconds later the actual response (optional, disabled by default - uses "enable_delayed_responses" property); More frequent opponent gap reports on longer tracks.

Version 4.1.6.3: Added Raceroom Formula Junior class; Tweaked Raceroom engine damage thresholds.

Version 4.1.6.2: Some TTS revisions; Updated RaceRoom car classes to match new patch.

Version 4.1.6.1: Some TTS changes so the app should use Microsoft's David voice on Windows 10 (Windows 7 users are stuck with the execrable Anna); Some gamer tag -> driver name extraction tweaks.

Version 4.1.6.0: Fixed crash bug when selecting 'alternate beeps'; Some Project Cars session restart detection changes; Work in progress text-to-speech for missing driver name.

Version 4.1.5.0: Added missing position messages for positions greater than 24.

Version 4.1.4.5: Disable PCars pit window messages by default (can be re-enabled with the enable_pcars_pit_window_messages setting) - this only works correctly in offline races; Revised some of the PCars session-end logic to reduce the likelihood of the app detecting a session restart when one hasn't actually taken place. This should also prevent the app from removing cached laptime data (which results in inaccurate 'best lap' messages).

Version 4.1.4.4: More pit window logic fixes for PCars; don't play pre-lights messages in PCars when the race is a fixed time.

Version 4.1.4.3: Fixed 'box this lap' calls being made when there is no mandatory stop, when running PCars in UDP mode.

Version 4.1.4.2: Fixed some speech recogniser / button handling issues - "Toggle" mode is now renamed "Press and release button" and actually works; Read the sector times response as a single message per sector, to allow interrupting and fix an issue with the Italian number reader.

Version 4.1.4.1: Fixed missing sector 3 time being read as "zero tenths off the pace".

Version 4.1.4.0: Reworked sector delta reporting to provide actual deltas, rather than approximations; Some changes to the Italian number reader (still work in progress); Some bug fixes.

Version 4.1.3.2: Removed some debug code that shouldn't have made it into the release.

Version 4.1.3.1: A couple of internal fixes.
	
Version 4.1.3.0: Added language-specific sound pack stuff; Better support for language specific number and time speech generation; Some internal bug fixing; Don't play wheel locking warnings if the player has a missing wheel or puncture; Don't play laptime improving / worsening messages if the conditions have significantly changed (rain or track temp); Don't play a message twice in succession if a player asks for something that the app was going to tell them anyway; Don't play good / OK start messages if the player has picked up a penalty (i.e. false start);Insert a short pause between some messages;Reduce the likelihood of multiple sweary messages being played in quick succession;Some better error trapping when the app is closed.

Version 4.1.2.2: Fixed radio channel (hold) button function for PCars network data.

Version 4.1.2.1: Added some car class data and pit detection points for the PCars Lotus DLC; Fixed some pit detection issues in PCars; Added option to enable spotter in hot lap (time trial) mode for PCars; Don't play lap time messages when we're in the pit lane;Don't complain about worsening lap times if the player has made a pass on this lap

Version 4.1.2.0: Major speech recognizer overhaul to allow customisation; Externalised all UI text; Added some options to number reading; Fixes to Hot Lap (timetrial) mode in PCars; Don't trigger flags event when stationary; A couple of internal bug fixes

Version 4.1.1.4: Added some car classes and Bannockbrae track for PCars; Remove stale opponents in PCars; Some internal error handling

Version 4.1.1.3: Allow messages with optional prefixes / suffixes to play without their prefixes or suffixes; Tidied up String encoding handling; Reverted console logging change (after a couple of attempts - hence the version number jump)

Version 4.1.1.0: Better selection of sound files from those available for each message - should give less repetition;Made the console logging a bit more efficient; Some String encoding rework for PCars. PS4 users should use UTF-8 for the pcars_character_encoding property, XBox and PC should use windows-1252; Added PCars V8 Supercar to car classes (more to come here); Fixed last-lap message for R3E timed races (should now work when you're not leading)

Version 4.1.0.3: Fixed possible bug in pit detection that could cause repeated messages; Added 'can you hear me' speech recognition to check it's working (should respond with 'yes, I can hear you'); Take start position into account when generating race end message; A couple of internal bug fixes; A few sound pack tweaks to make the personalisation sounds work a little better

Version 4.1.0.2: Renamed UDP network button data option to make it clearer that this takes button presses from the UDP stream, rather than from the device directly

Version 4.1.0.1: More internal fixes to the radio channel handling logic to handle a couple of edge-cases where it wasn't closing the channel promptly; Spotter performance and latency improvements; Spotter logic fixes for cases where a '3 wide' turns into a 'car left' / 'car right'; Don't attempt to update and load a new driver name for an existing player if the new name isn't valid / usable; Tyre temp range tweaks; Check messages for validity and timeout just before playing them; Use separate class for each PCars Road car class; Handle broken PCars string data which had null characters in the middle of the String; PCars car class handling improvements

Version 4.1.0.0: Internal audio handling overhaul - better queue handling, smarter caching of sound objects, more reliable radio channel state management (should prevent channel being left open); Added support for personalised message prefixes and suffixes; Spotter fix - reinstated missing width separation check to prevent spotter calls being made when a car is directly in front / behind but within the car length parameter; Internal audio handling overhaul - better queue handling, more reliable radio channel state management (should prevent channel being left open); Fixed number reading for some numbers; Fixed DTM 2014 tyre compound error in the 'box now' message; Validate overtake messages to ensure they're not out of date by the time they're played

Version 4.0.3.5: Fixed major regression for Project Cars - hold all internal Strings as raw byte arrays (which may or may not have a null first character) and decode them when we need them

Version 4.0.3.4: Internal rework for Project Cars to handle String data which occasionally starts with a null character. Should fix 'missing' opponents and incorrect car classes

Version 4.0.3.3: Major spotter overhaul - changed the way app calculates opponent speeds, much more accurate. Should make a difference to the ghost calls

Version 4.0.3.2: Overtaking messages tweak - make these a bit more likely; Increased some brake temp thresholds: Fixed "what's my best lap time" response; Stop the autoupdater running when the app starts listening for data; Added packet rate estimate to console output for PCars Network data

Version 4.0.3.1: Fixed startup bug on initial install; Some fuel useage warning rework

Version 4.0.3.0: Fixed overtaking messages in PCars (caused by noise in the opponent speed data - this is now based on a sliding average); Fixed baseline engine temperature calculations for RaceRoom; Corrected brake temp thresholds and engine damage thresholds; Some internal bug fixes in the spotter and numeric message handling; Do auto update checks in a background Thread; Fixed session time left reporting

Version 4.0.2.0: Added optional default sound pack installation location override property ('override_default_sound_pack_location'); Fixed RaceRoom spotter ghost calls at some tracks; reworked laptime comparisons for practice and qual sessions; fixed "where's p X" response.

Version 4.0.1.0: Fixed sound pack installation location - this now uses /Users/[username]/AppData/Local/CrewChiefV4/sounds

Version 4.0.0.0: Initial release of version 4. The app now comes packaged as a single auto-updating .msi installer and includes integrated sound and driver names pack updating. The spotter has been overhauled, brake temp messages fixed, and car class and driver names for RaceRoom Formula 2 drivers have been added.
