[![Build status](https://ci.appveyor.com/api/projects/status/2ht05c7bpshybpe5?svg=true)](https://ci.appveyor.com/project/TonyWhitley/crewchiefv4/branch/HTMLhelp)

To simplify creating the content for HTML help files HelpFiles.exe inserts each content HTML file into menu.HTML to create the published pages.

The process is automatic when a change is submitted to master that has the commit message `update Help`, AppVeyor (see appveyor.yml) is used to build HelpFiles.exe which create the HTML files and commits the results in /public from where they will be published in a GitLab Pages website 
(https://mr_belowski.gitlab.io/CrewChiefV4/index.html) by the GitLab CI/CD on the main branch (see .gitlab-ci.yml). Also, change_log_for_auto_updated.html is used to create the **About/Change log** page.

**To add a Page**
- Edit menu.html (I recommend using a text editor and just cut and paste an existing entry)
- Add the page to the list ```pageNames``` in HelpFiles/program.cs

**To add an Image**
- Add the image to the HelpFiles folder
- Add the image to the list ```images``` in HelpFiles/program.cs

**To add a Game**
- Add the game to the list ```gameNames``` in HelpFiles/program.cs
- Add a page *GettingStarted_GameSpecific_[gameName]*
- Add a page *GameSpecific_ForEachGame_[gameName]*
