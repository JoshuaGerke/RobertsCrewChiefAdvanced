@echo off
cls
setlocal

rem *******************************************************
rem How to use:
rem
rem Download this file to anywhere convenient (like your desktop) and
rem simply double-click it instead of starting Crew Chief before playing any sim.
rem 
rem When you start a sim in GAMESLIST it will start up Crew Chief, pre-selecting
rem the game for you.  When you exit the sim it will close down Crew Chief and
rem wait until you load another sim.  When you've finished racing press Ctrl/C
rem to exit this program.
rem 
rem You can probably use it as it is but if you have set up your computer
rem differently to me you may need to edit the "MAIN USER-CONFIGURABLE ITEMS"
rem below (edit in Notepad for example).  If you're unlucky you may have to
rem edit one of the "ITEMS YOU'RE LESS LIKELY TO NEED TO CHANGE".
rem 
rem If it doesn't work go to LOGGING MAY HELP IF THERE ARE PROBLEMS and
rem set logging=on
rem This will write a log file which *may* help diagnosing the problem.
rem
rem If it still doesn't work... see the warranty below
rem *******************************************************


REM *******************************************************
REM NO WARRANTY IMPLIED OR GIVEN. YMMV.
REM OBJECTS IN THE MIRROR ARE CLOSER THAN THEY APPEAR.
REM ETC. ETC.  YOU'RE ON YOUR OWN!
REM *******************************************************


rem *******************************************************
rem MAIN USER-CONFIGURABLE ITEMS:

rem If you want you can trim this list to only check for the games you have
set GAMESLIST=ASSETTO_64BIT,ACC,AMS,AMS2,DR2,GTR2,IRACING,LMU,PCARS2,RF2,RACE_ROOM

rem Where Crew Chief is installed
set CrewChiefPath="%ProgramFiles(x86)%\Britton IT Ltd\CrewChiefV4"

rem *******************************************************

rem *******************************************************
rem ITEMS YOU'RE LESS LIKELY TO NEED TO CHANGE

	set ASSETTO_64BIT_process=assettocorsa.exe
	set ACC_process=acc.exe
	set AMS_process=AMS.exe
	set AMS2_process=AMS2AVX.exe
	set DR2_process=dirtrally2.exe
	set GTR2_process=GTR2.exe
	set IRACING_process=iRacingSim64DX11.exe
	set "LMU_process=Le Mans Ultimate.exe"
	set PCARS2_process=pcars2avx.exe
	set RF2_process=rfactor2.exe
	set RACE_ROOM_process=RRRE64.exe
rem *******************************************************

rem LOGGING MAY HELP IF THERE ARE PROBLEMS
set logging=off


rem ******************* HERE BE DRAGONS *******************	
rem Shouldn't need to touch anything below

	setlocal enableDelayedExpansion
	call :logSetup

title %~nx0
:Loop	
	@echo Program to check if one of a list of games has been started and
	@echo start Crew Chief for that game.
	@echo Also closes it down when the game finishes.
	@echo.
	@echo Press Ctrl/C to stop
	@echo.
	for %%g in (%gamesList%) do call :doGame %%g
	rem wait before checking again
	timeout /t 5 > nul
	cls
	goto :Loop


:doGame
	set game=%1
	set "process=%1_process"
	set "PROCESS_NAME=!%1_process!
	set "PROCESS_NAME=%PROCESS_NAME:~0,12%" 
	@echo Checking for %game% starting (!%process%!)...
	::%SystemRoot%\System32\qprocess.exe !%process%! >nul 2>&1
	qprocess | findstr /I /C:"%PROCESS_NAME%" >nul 2>&1
	if errorlevel 1 goto :NextGame
	@call :cmnt %game% started
	@call :%game%banner
	@call :doCrewChief
	@call :cmnt Now waiting for %game% to close down after playing...
:WaitStop
	::%SystemRoot%\System32\qprocess.exe !%process%! >nul 2>&1
	qprocess | findstr /I /C:"%PROCESS_NAME%" >nul 2>&1
	if errorlevel 1 (
		call :cmnt %game% finished
		goto :shutDown
		) else (
		timeout /t 2 > nul
		goto :WaitStop
		)
:shutDown
	call :cmnt Shut down CC
	@call :log start /d %CrewChiefPath% CrewChiefV4.exe -c_exit
	start /d %CrewChiefPath% CrewChiefV4.exe -c_exit
:NextGame

	


	goto :eof

::::::::::::::::::::::::::::::::::::::::::::::::::

:doCrewChief
	call :CrewChief  -game %game% -profile %game%
	goto :eof
:CrewChief
	@call :log start /d %CrewChiefPath% CrewChiefV4.exe %*
	if /i "%2" == "None" goto :eof
	start /d %CrewChiefPath% CrewChiefV4.exe %*
	goto :eof


::::::::::::::::::::::::::::::::::::::::::::::::::
:logSetup
	if /i '%logging%' == 'off' GOTO :eof
	
	rem Log file timestamp
	set CUR_YYYY=%date:~10,4%
	set CUR_MM=%date:~7,2%
	set CUR_DD=%date:~4,2%
	set CUR_HH=%time:~0,2%
	if %CUR_HH% lss 10 (set CUR_HH=0%time:~1,1%)

	set CUR_NN=%time:~3,2%
	set CUR_SS=%time:~6,2%
	set CUR_MS=%time:~9,2%

	if not exist %USERPROFILE%\documents\CrewChief_AutoSwitcher mkdir %USERPROFILE%\documents\CrewChief_AutoSwitcher > nul
	set log=%USERPROFILE%\documents\CrewChief_AutoSwitcher\%CUR_YYYY%%CUR_MM%%CUR_DD%-%CUR_HH%%CUR_NN%%CUR_SS%.log
	call :cmnt Log file %log%

	echo.%date% > %log%
	goto :eof
::::::::::::::::::::::::::::::::::::::::::::::::
rem Echo and log comments	
:cmnt
	echo %*
	set _type=CMT
	goto :logIt
rem Log logs
:log
	set _type=LOG
:logIt	
	if /i '%logging%' == 'off' GOTO :eof
	echo %time% - %_type% %* >> %log%
	goto :eof
	
::::::::::::::::::::::::::::::::::::::::::::::::
  ____                                  
 |  _ \                                 
 | |_) | __ _ _ __  _ __   ___ _ __ ___ 
 |  _ < / _` | '_ \| '_ \ / _ \ '__/ __|
 | |_) | (_| | | | | | | |  __/ |  \__ \
 |____/ \__,_|_| |_|_| |_|\___|_|  |___/

	https://www.coolgenerator.com/ascii-text-generator "Big" font

:rf2Banner
echo        ______         _               ___  
echo       ^|  ____^|       ^| ^|             ^|__ \ 
echo   _ __^| ^|__ __ _  ___^| ^|_ ___  _ __     ) ^|
echo  ^| '__^|  __/ _` ^|/ __^| __/ _ \^| '__^|   / / 
echo  ^| ^|  ^| ^| ^| (_^| ^| (__^| ^|^| (_) ^| ^|     / /_ 
echo  ^|_^|  ^|_^|  \__,_^|\___^|\__\___/^|_^|    ^|____^|
echo.
goto :eof

:ASSETTO_64BITbanner
echo                         _   _           _____                     
echo      /\                ^| ^| ^| ^|         / ____^|                    
echo     /  \   ___ ___  ___^| ^|_^| ^|_ ___   ^| ^|     ___  _ __ ___  __ _ 
echo    / /\ \ / __/ __^|/ _ \ __^| __/ _ \  ^| ^|    / _ \^| '__/ __^|/ _` ^|
echo   / ____ \\__ \__ \  __/ ^|_^| ^|^| (_) ^| ^| ^|___^| (_) ^| ^|  \__ \ (_^| ^|
echo  /_/    \_\___/___/\___^|\__^|\__\___/   \_____\___/^|_^|  ^|___/\__,_^|
echo.
goto :eof 

:ACCbanner

echo               _____    _____ 
echo      /\      / ____^|  / ____^|
echo     /  \    ^| ^|      ^| ^|     
echo    / /\ \   ^| ^|      ^| ^|     
echo   / ____ \  ^| ^|____  ^| ^|____ 
echo  /_/    \_\  \_____^|  \_____^|
echo.
goto :eof

:AMSbanner
echo                 _                        _     _ _ _     _        
echo      /\        ^| ^|                      ^| ^|   (_) (_)   ^| ^|       
echo     /  \  _   _^| ^|_ ___  _ __ ___   ___ ^| ^|__  _^| ^|_ ___^| ^|_ __ _ 
echo    / /\ \^| ^| ^| ^| __/ _ \^| '_ ` _ \ / _ \^| '_ \^| ^| ^| / __^| __/ _` ^|
echo   / ____ \ ^|_^| ^| ^|^| (_) ^| ^| ^| ^| ^| ^| (_) ^| ^|_) ^| ^| ^| \__ \ ^|^| (_^| ^|
echo  /_/    \_\__,_^|\__\___/^|_^| ^|_^| ^|_^|\___/^|_.__/^|_^|_^|_^|___/\__\__,_^|
echo.
goto :eof

:AMS2banner
echo                 _                        _     _ _ _     _          ___  
echo      /\        ^| ^|                      ^| ^|   (_) (_)   ^| ^|        ^|__ \ 
echo     /  \  _   _^| ^|_ ___  _ __ ___   ___ ^| ^|__  _^| ^|_ ___^| ^|_ __ _     ) ^|
echo    / /\ \^| ^| ^| ^| __/ _ \^| '_ ` _ \ / _ \^| '_ \^| ^| ^| / __^| __/ _` ^|   / / 
echo   / ____ \ ^|_^| ^| ^|^| (_) ^| ^| ^| ^| ^| ^| (_) ^| ^|_) ^| ^| ^| \__ \ ^|^| (_^| ^|  / /_ 
echo  /_/    \_\__,_^|\__\___/^|_^| ^|_^| ^|_^|\___/^|_.__/^|_^|_^|_^|___/\__\__,_^| ^|____^|	
echo.
goto :eof

:IRACINGbanner
echo    _ _____            _             
echo   (_)  __ \          (_)            
echo    _^| ^|__) ^|__ _  ___ _ _ __   __ _ 
c^| ^|  _  // _` ^|/ __^| ^| '_ \ / _` ^|
echo   ^| ^| ^| \ \ (_^| ^| (__^| ^| ^| ^| ^| (_^| ^|
echo   ^|_^|_^|  \_\__,_^|\___^|_^|_^| ^|_^|\__, ^|
echo                                __/ ^|
echo                               ^|___/ 
goto :eof

:LMUbanner
echo     _            __  __                   _    _ _ _   _                 _       
echo    ^| ^|          ^|  \/  ^|                 ^| ^|  ^| ^| ^| ^| (_)               ^| ^|      
echo    ^| ^|     ___  ^| \  / ^| __ _ _ __  ___  ^| ^|  ^| ^| ^| ^|_ _ _ __ ___   __ _^| ^|_ ___ 
echo    ^| ^|    / _ \ ^| ^|\/^| ^|/ _` ^| '_ \/ __^| ^| ^|  ^| ^| ^| __^| ^| '_ ` _ \ / _` ^| __/ _ \
echo    ^| ^|___^|  __/ ^| ^|  ^| ^| (_^| ^| ^| ^| \__ \ ^| ^|__^| ^| ^| ^|_^| ^| ^| ^| ^| ^| ^| (_^| ^| ^|^|  __/
echo    ^|______\___^| ^|_^|  ^|_^|\__,_^|_^| ^|_^|___/  \____/^|_^|\__^|_^|_^| ^|_^| ^|_^|\__,_^|\__\___^|
goto :eof

:PCARS2banner
echo   _____           _           _      _____                 ___  
echo  ^|  __ \         (_)         ^| ^|    / ____^|               ^|__ \ 
echo  ^| ^|__) ^| __ ___  _  ___  ___^| ^|_  ^| ^|     __ _ _ __ ___     ) ^|
echo  ^|  ___/ '__/ _ \^| ^|/ _ \/ __^| __^| ^| ^|    / _` ^| '__/ __^|   / / 
echo  ^| ^|   ^| ^| ^| (_) ^| ^|  __/ (__^| ^|_  ^| ^|___^| (_^| ^| ^|  \__ \  / /_ 
echo  ^|_^|   ^|_^|  \___/^| ^|\___^|\___^|\__^|  \_____\__,_^|_^|  ^|___/ ^|____^|
echo                 _/ ^|                                            
echo                ^|__/ 
echo.
goto :eof

:RACE_ROOMbanner
echo   _____                  _____                       
echo  ^|  __ \                ^|  __ \                      
echo  ^| ^|__) ^|__ _  ___ ___  ^| ^|__) ^|___   ___  _ __ ___  
echo  ^|  _  // _` ^|/ __/ _ \ ^|  _  // _ \ / _ \^| '_ ` _ \ 
echo  ^| ^| \ \ (_^| ^| (_^|  __/ ^| ^| \ \ (_) ^| (_) ^| ^| ^| ^| ^| ^|
echo  ^|_^|  \_\__,_^|\___\___^| ^|_^|  \_\___/ \___/^|_^| ^|_^| ^|_^|
echo.
goto :eof
 
:F1_2015Banner
echo   ______ __    ___   ___  __ _____ 
echo  ^|  ____/_ ^|  ^|__ \ / _ \/_ ^| ____^|
echo  ^| ^|__   ^| ^|     ) ^| ^| ^| ^|^| ^| ^|__  
echo  ^|  __^|  ^| ^|    / /^| ^| ^| ^|^| ^|___ \ 
echo  ^| ^|     ^| ^|   / /_^| ^|_^| ^|^| ^|___) ^|
echo  ^|_^|     ^|_^|  ^|____^|\___/ ^|_^|____/ 
echo.
goto :eof

:F1_2019Banner
echo   ______ __    ___   ___  __  ___  
echo  ^|  ____/_ ^|  ^|__ \ / _ \/_ ^|/ _ \ 
echo  ^| ^|__   ^| ^|     ) ^| ^| ^| ^|^| ^| (_) ^|
echo  ^|  __^|  ^| ^|    / /^| ^| ^| ^|^| ^|\__, ^|
echo  ^| ^|     ^| ^|   / /_^| ^|_^| ^|^| ^|  / / 
echo  ^|_^|     ^|_^|  ^|____^|\___/ ^|_^| /_/  
echo.
goto :eof

:DR2Banner
echo   _____  _      _     _____       _ _         ___  
echo  ^|  __ \(_)    ^| ^|   ^|  __ \     ^| ^| ^|       ^|__ \ 
echo  ^| ^|  ^| ^|_ _ __^| ^|_  ^| ^|__) ^|__ _^| ^| ^|_   _     ) ^|
echo  ^| ^|  ^| ^| ^| '__^| __^| ^|  _  // _` ^| ^| ^| ^| ^| ^|   / / 
echo  ^| ^|__^| ^| ^| ^|  ^| ^|_  ^| ^| \ \ (_^| ^| ^| ^| ^|_^| ^|  / /_ 
echo  ^|_____/^|_^|_^|   \__^| ^|_^|  \_\__,_^|_^|_^|\__, ^| ^|____^|
echo                                        __/ ^|       
echo                                       ^|___/
echo.
goto :eof

:GTR2Banner
echo    _____ _______ _____  ___  
echo   / ____^|__   __^|  __ \^|__ \ 
echo  ^| ^|  __   ^| ^|  ^| ^|__) ^|  ) ^|
echo  ^| ^| ^|_ ^|  ^| ^|  ^|  _  /  / / 
echo  ^| ^|__^| ^|  ^| ^|  ^| ^| \ \ / /_ 
echo   \_____^|  ^|_^|  ^|_^|  \_\____^|
echo.
goto :eof
