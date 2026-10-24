rem A batch file to download and install the sound files to Crew Chief's default folder. 
rem This works for me but there are no guarantees it will work for you

setlocal

rem This is the preferred URL
set url=http://167.235.144.28/

rem Alternate download URL
rem set url=https://thecrewchief.org/downloads/

set z7="C:\Program Files\7-Zip\7z.exe"

rem Download the zip files to %LOCALAPPDATA%CrewChiefV4\soundpacks
rem then extract the sound files to %LOCALAPPDATA%CrewChiefV4\sounds
pushd %LOCALAPPDATA%
if not exist CrewChiefV4 md CrewChiefV4 
cd CrewChiefV4 
if not exist soundpacks md soundpacks
cd soundpacks

for %%f in (		
		base_sound_pack.zip
		update_sound_pack.zip
		update_2_sound_pack.zip
		update_3_sound_pack.zip
		update_4_sound_pack.zip
		update_5_sound_pack.zip
		update_6_sound_pack.zip
		update_7_sound_pack.zip
		update_8_sound_pack.zip
		update_9_sound_pack.zip
		update_10_sound_pack.zip
		update_11_sound_pack.zip
		update_12_sound_pack.zip
		update_13_sound_pack.zip
		update_14_sound_pack.zip
		update_15_sound_pack.zip
		update_16_sound_pack.zip
		update_17_sound_pack.zip
		update_18_sound_pack.zip
		update_19_sound_pack.zip
		update_20_sound_pack.zip
		update_21_sound_pack.zip
		update_22_sound_pack.zip
		update_23_sound_pack.zip
		update_24_sound_pack.zip
		update_25_sound_pack.zip
		update_26_sound_pack.zip
		update_27_sound_pack.zip
		update_28_sound_pack.zip
		update_29_sound_pack.zip
		update_30_sound_pack.zip
		update_31_sound_pack.zip
		update_32_sound_pack.zip
		update_33_sound_pack.zip
		update_34_sound_pack.zip
		update_35_sound_pack.zip
		update_36_sound_pack.zip
		update_37_sound_pack.zip
		update_38_sound_pack.zip
		update_193_sound_pack.zip
		update_194_sound_pack.zip
		update_195_sound_pack.zip
		update_196_sound_pack.zip
		update_197_sound_pack.zip
		update_198_sound_pack.zip
		) do (if not exist %%f curl %url%%%f -o%%f
		%z7% x -aos -o..\sounds %%f)

for %%f in (		
		base_driver_names.zip
		update_driver_names.zip
		update_2_driver_names.zip
		update_3_driver_names.zip
		update_4_driver_names.zip
		update_5_driver_names.zip
		update_6_driver_names.zip
		update_7_driver_names.zip
		update_8_driver_names.zip
		update_9_driver_names.zip
		update_10_driver_names.zip
		update_11_driver_names.zip
		update_12_driver_names.zip
		update_13_driver_names.zip
		update_144_driver_names.zip
		) do (if not exist %%f curl %url%%%f -o%%f
		%z7% x -aos -o..\sounds %%f)

for %%f in (		
		personalisations.zip
		update_personalisations.zip
		update_2_personalisations.zip
		update_3_personalisations.zip
		update_4_personalisations.zip
		update_5_personalisations.zip
		update_6_personalisations.zip
		update_7_personalisations.zip
		update_8_personalisations.zip
		update_9_personalisations.zip
		update_10_personalisations.zip
		update_11_personalisations.zip
		update_12_personalisations.zip
		update_13_personalisations.zip
		update_14_personalisations.zip
		update_15_personalisations.zip
		update_16_personalisations.zip
		update_17_personalisations.zip
		update_18_personalisations.zip
		update_19_personalisations.zip
		) do (if not exist %%f curl %url%%%f -o%%f
		%z7% x -aos -o..\sounds\personalisations %%f)
