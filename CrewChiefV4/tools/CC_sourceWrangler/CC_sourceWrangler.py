import os
import re
import subprocess

"""
Script to update Crew Chief Events files to use SpeechCommands.ID
1) Replace SpeechRecogniser.ResultContains(...) with cmd == SpeechCommands.ID.(...)  and write to filename.cs.new
2) Assemble the list of commands present in the filename.cs and print them for the author to copy and paste into filename.cs.new
3) Ditto for ControllerConfiguration equivalents
4) Ditto for StartsWith\(SpeechRecogniser
5) Ditto for EndsWith  though the value needs to be calculated in filename.cs.new
6) Open a diff utility (I use Beyond Compare, change "compare" to your preference) for filename.cs filename.cs.new
7) Author saves filename.cs.new as filename.cs

SpeechCommands.ID cmd = this.Id;  is assumed to have been calculated in SpeechRecogniser and passed via AbstractEvent
"""
class Wrangler:
    specialCases = ["LAP", "MINUTE", "HOUR", "LAPS", "MINUTES", "HOURS", "LITERS", "GALLONS"] # used by CALCULATE_FUEL_FOR
    lines = []
    StartsWith = []
    EndsWith = []
    filename = r"d:\Tony\repos\CrewChiefV4.HTMLHelp\CrewChiefV4\Events\Opponents.cs" # for testing
    compare = r"C:\Program Files\Beyond Compare 4\BCompare.exe"
    def ReadFile(self):
        with open(self.filename, "r") as fp:
            self.lines = fp.readlines()
        return self.lines
    def ExtractSpeechRecogniser(self):
        regex = r"SpeechRecogniser.ResultContains\(.*?, SpeechRecogniser\.(.*?)\)"
        self.SpeechRecognisers = []
        for line in self.lines:
            matches = re.search(regex, line)
            if matches:
                m = matches.group(1)
                if not m in self.SpeechRecognisers and not m in self.specialCases:
                    self.SpeechRecognisers.append(m)
        # This is not accurate enough
        # regex = r"SpeechRecogniser\.(.*?).Contains.*"
        # for line in self.lines:
        #     matches = re.search(regex, line)
        #     if matches:
        #         m = matches.group(1)
        #         if not m in self.SpeechRecognisers and not m in self.specialCases:
        #             self.SpeechRecognisers.append(m)
        return self.SpeechRecognisers

    def ExtractSpeechRecogniserController(self):
        regex = r".*SpeechRecogniser.ResultContains\(.*?, new string\[\] *\{ *ControllerConfiguration\.(.*?)\)"
        self.SpeechRecognisersController = []
        for line in self.lines:
            matches = re.search(regex, line)
            if matches:
                self.SpeechRecognisersController.append(matches.group(1))
        return self.SpeechRecognisersController

    def ExtractStartsWith(self):
        regex = r"\.StartsWith\(SpeechRecogniser\.(.*?)\) XXXXXXXXXX"
        self.StartsWith = []
        for line in self.lines:
            matches = re.search(regex, line)
            if matches:
                self.StartsWith.append(matches.group(1))
        return self.StartsWith

    def ExtractEndsWith(self):
        regex = r"\.EndsWith\(SpeechRecogniser\.(.*?)\) XXXXXXXXXXXXXX"
        self.EndsWith = []
        for line in self.lines:
            matches = re.search(regex, line)
            if matches:
                self.EndsWith.append(matches.group(1))
        return self.EndsWith


    def ReplaceSpeechRecogniser(self):
        regex = r"SpeechRecogniser.ResultContains\(.*?, SpeechRecogniser\.(.*?)\)"
        for lineNum, line in enumerate(self.lines):
            matches = re.search(regex, line)
            if matches and not matches.group(1) in self.specialCases:
                self.lines[lineNum] = re.sub(regex, f"cmd == SpeechCommands.ID.{matches.group(1)}", line)
                # print(self.lines[lineNum])
        # This is not accurate enough
        # regex = r"SpeechRecogniser\.(.*?).Contains.*"
        # for lineNum, line in enumerate(self.lines):
        #     matches = re.search(regex, line)
        #     if matches and not matches.group(1) in self.specialCases:
        #         self.lines[lineNum] = re.sub(regex, f"cmd == SpeechCommands.ID.{matches.group(1)}\)", line)

    def ReplaceSpeechRecogniserController(self):
        regex = r"SpeechRecogniser.ResultContains\(.*?, new string\[\] *\{ *ControllerConfiguration\.(.*?)\)"
        for lineNum, line in enumerate(self.lines):
            matches = re.search(regex, line)
            if matches:
                self.lines[lineNum] = re.sub(regex, f"cmd ==SpeechCommands.ID.{matches.group(1)}", line)
                # print(self.lines[lineNum])

    def ReplaceStartsWith(self):
        regex = r"voiceMessage\.StartsWith\(SpeechRecogniser\.(.*?)\)"
        for lineNum, line in enumerate(self.lines):
            while True:
                matches = re.search(regex, line)
                if matches:
                    line = re.sub(regex, f"startsWith == SpeechCommands.ID.StartsWith_{matches.group(1)}", line, 1)
                    self.lines[lineNum] = line
                else:
                    break;

    def ReplaceEndsWith(self):
        regex = r"voiceMessage\.EndsWith\(SpeechRecogniser\.(.*?)\)"
        for lineNum, line in enumerate(self.lines):
            while True:
                matches = re.search(regex, line)
                if matches:
                    line = re.sub(regex, f"endsWith == SpeechCommands.ID.EndsWith_{matches.group(1)}", line, 1)
                    self.lines[lineNum] = line
                else:
                    break;

    def WriteFile(self):
        with open(self.filename, "w") as fp:
            fp.writelines(self.lines)

    def CreateSpeechCommands(self):
        speechCommands = []
        if len(self.SpeechRecognisers) > 0:
            speechCommands = ["        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>\n",
                              "        {\n"]
            for speechRecogniser in sorted(self.SpeechRecognisers):
                speechCommands.append(f"            SpeechCommands.ID.{speechRecogniser},\n")
            speechCommands.append("        };\n")
            speechCommands.extend([
              "        public override AbstractEvent HandlesEvent(String voiceMessage)\n",
              "        {\n",
              "            return this.handlesEvent(voiceMessage, Commands);\n",
              "        }\n",]
            )
        if len(self.SpeechRecognisersController) > 0:
            speechCommands.append("        public static List<SpeechCommands.ID> ControllerCommands = new List<SpeechCommands.ID>\n")
            speechCommands.append("        {\n")
            for speechRecogniser in sorted(self.SpeechRecognisersController):
                speechCommands.append(f"            SpeechCommands.ID.{speechRecogniser},\n")
            speechCommands.append("        };\n")
        return speechCommands

    def InsertSpeechCommands(self, speechCommands):
        if len(speechCommands) == 0:
            return
        # find public override void respond(String voiceMessage)
        regex = r"( *public override void respond\([^,]*)\)"
        newfile = []
        respondFound = False
        for line in self.lines:
            if respondFound:
                newfile.append("        public override SpeechCommands.ID HandlesEvent(String voiceMessage)\n")
                newfile.append("        {\n")
                newfile.append("            return SpeechCommands.SpeechToCommand(Commands, voiceMessage)\n")
                newfile.append("        }\n")
                newfile.append("\n")
                newfile.append("        public override void respond(String voiceMessage)\n")
                newfile.append("        {\n")
                newfile.append("            respond(voiceMessage, HandlesEvent(voiceMessage))\n")
                newfile.append("        }\n")
                newfile.append("        public override void respond(String voiceMessage, SpeechCommands.ID cmd)\n")
                newfile.append("        {\n")
                newfile.append("            if (!Commands.Contains(cmd))\n")
                newfile.append("            {\n")
                newfile.append('                Log.Error($"{cmd.ToString()} not handled by {this.GetType().Name}")\n')
                newfile.append("                return;\n")
                newfile.append("            }\n")

                if len(self.StartsWith) > 0:
                    newfile.append('        SpeechCommands.ID startsWith = voiceMessage.StartsWith("");\n')
                if len(self.EndsWith) > 0:
                    newfile.append('        SpeechCommands.ID endsWith = voiceMessage.EndsWith("");\n')
                respondFound = False
            else:
                matches = re.search(regex, line)
                if matches:
                    newfile.extend(speechCommands)
                    respondFound = True
                    #line = re.sub(regex, "\g<1>, SpeechCommands.ID cmd)", line);
                newfile.append(line)
        self.lines = newfile

def main():
    os.chdir(r"..\..\Events")
    files = [f for f in os.listdir() if os.path.isfile(f)]
    #files = ["CoDriver.cs"]

    for file in files:
        if files == "CommonActions.cs": next
        obj = Wrangler()
        # obj.filename=input(r"File? (use \ for paths) : ").strip()
        # if os.path.isfile(obj.filename):
        obj.filename = file
        obj.ReadFile()
        obj.ExtractSpeechRecogniser()
        obj.ExtractSpeechRecogniserController()
        #obj.ExtractStartsWith()
        #obj.ExtractEndsWith()

        speechCommands = obj.CreateSpeechCommands()
        # for line in speechCommands:
        #     print(line, end='')
        obj.ReplaceSpeechRecogniser()
        obj.ReplaceSpeechRecogniserController()
        #obj.ReplaceStartsWith()
        #obj.ReplaceEndsWith()
        obj.InsertSpeechCommands(speechCommands)
        obj.WriteFile()
        print(f"Wrote file {obj.filename}")
        #subprocess.run([obj.compare, obj.filename, obj.filename+".new"])


if __name__ == "__main__":
    main()
