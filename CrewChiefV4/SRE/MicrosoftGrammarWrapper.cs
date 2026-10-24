using Microsoft.Speech.Recognition;
using System;
using System.IO;
using System.Xml;
using Microsoft.Speech.Recognition.SrgsGrammar;

namespace CrewChiefV4.SRE
{
    class MicrosoftGrammarWrapper : GrammarWrapper
    {
        private Grammar grammar;
        public MicrosoftGrammarWrapper(GrammarBuilderWrapper grammarBuilderWrapper, string grammarName)
        {
            this.grammar = new Grammar((GrammarBuilder)grammarBuilderWrapper.GetInternalGrammarBuilder());
#if DUMP_GRAMMAR_FILES
            // this wil dump the SRE grammar object to the console (as a list of the phrases in all of its choices). 
            Log.SoundDebug(() => "Create grammar with contents\n " + ((GrammarBuilder)grammarBuilderWrapper.GetInternalGrammarBuilder()).DebugShowPhrases);

            // Not easy to share with SystemGrammarWrapper as there are different usings
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineOnAttributes = true;
            var dumpFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "dumpMicrosoftGrammars");
            System.IO.Directory.CreateDirectory(dumpFilesPath);
            var filename = Path.Combine(dumpFilesPath, $"{CrewChief.gameDefinition.gameEnum.ToString()}_{grammarName}.grxml");
            System.Xml.XmlWriter writer =
              System.Xml.XmlWriter.Create(filename, settings);
            var document = new SrgsDocument((GrammarBuilder)grammarBuilderWrapper.GetInternalGrammarBuilder());
            document.WriteSrgs(writer);
            writer.Close();
            var Phrases = ((GrammarBuilder)grammarBuilderWrapper.GetInternalGrammarBuilder()).DebugShowPhrases;
            // (format is slightly different to System version)
            filename = Path.ChangeExtension(filename, "txt");
            System.IO.File.WriteAllText(filename, Phrases);
            writer.Close();
#endif
        }

        public object GetInternalGrammar()
        {
            return grammar;
        }

        public bool Loaded()
        {
            return grammar.Loaded;
        }
    }
}
