import unittest
import CC_sourceWrangler

class Test_test_TDD(unittest.TestCase):
    def test_ReadFile(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        self.assertGreater(len(lines), 0)
        print(lines[0], lines[1], lines[2])
        
    def test_ExtractSpeechRecogniser(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        SpeechRecognisers = obj.ExtractSpeechRecogniser()
        self.assertGreater(len(SpeechRecognisers), 0)
        print(SpeechRecognisers[0], SpeechRecognisers[1], SpeechRecognisers[2])
        
    def test_ExtractStartsWith(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        StartsWiths = obj.ExtractStartsWith()
        self.assertGreater(len(StartsWiths), 0)
        print(StartsWiths[0], StartsWiths[1], StartsWiths[2])
        
    def test_ExtractEndsWith(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        EndsWiths = obj.ExtractEndsWith()
        self.assertGreater(len(EndsWiths), 0)
        print(EndsWiths[0], EndsWiths[1], EndsWiths[2])
        
    def test_ReplaceSpeechRecogniser(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        SpeechRecognisers = obj.ExtractSpeechRecogniser()
        self.assertGreater(len(SpeechRecognisers), 0)
        obj.ReplaceSpeechRecogniser()
        obj.WriteFile()
    def test_CreateSpeechCommands(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        SpeechRecognisers = obj.ExtractSpeechRecogniser()
        self.assertGreater(len(SpeechRecognisers), 0)
        speechCommands = obj.CreateSpeechCommands()
        for line in speechCommands:
            print(line)

    def test_FinalEdit(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        SpeechRecognisers = obj.ExtractSpeechRecogniser()
        SpeechRecognisersController = obj.ExtractSpeechRecogniserController()
        obj.ExtractStartsWith()
        obj.ExtractEndsWith()
        self.assertGreater(len(SpeechRecognisers), 0)
        speechCommands = obj.CreateSpeechCommands()
        obj.InsertSpeechCommands(speechCommands)
        obj.ReplaceSpeechRecogniser()
        obj.ReplaceSpeechRecogniserController()
        for line in obj.lines:
            print(line, end='')
        pass

    def test_ReplaceEndsWith(self):
        obj = CC_sourceWrangler.Wrangler()
        lines = obj.ReadFile()
        EndsWiths = obj.ExtractEndsWith()
        self.assertGreater(len(EndsWiths), 0)
        obj.ReplaceEndsWith()
        obj.WriteFile()
        

if __name__ == '__main__':
    unittest.main()
