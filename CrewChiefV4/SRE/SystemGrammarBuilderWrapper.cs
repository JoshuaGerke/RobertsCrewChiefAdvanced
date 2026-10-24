using System;
using System.Globalization;
using System.Speech.Recognition;

namespace CrewChiefV4.SRE
{
    class SystemGrammarBuilderWrapper : GrammarBuilderWrapper
    {
        private GrammarBuilder grammarBuilder;
        private CultureInfo cultureInfo = null;

        public SystemGrammarBuilderWrapper()
        {
            this.grammarBuilder = new GrammarBuilder();
        }

        public SystemGrammarBuilderWrapper(ChoicesWrapper choicesWrapper)
        {
            this.grammarBuilder = new GrammarBuilder((Choices)choicesWrapper.GetInternalChoices());
        }

        void GrammarBuilderWrapper.Append(ChoicesWrapper choicesWrapper)
        {
            grammarBuilder.Append((Choices)choicesWrapper.GetInternalChoices());
        }

        void GrammarBuilderWrapper.Append(ChoicesWrapper choicesWrapper, int minRepeat, int maxRepeat)
        {
            if (this.cultureInfo == null)
            {
                throw new InvalidOperationException("No culture object defined for nested GrammarBuilder");
            }
            GrammarBuilder choicesGrammarBuilder = new GrammarBuilder((Choices)choicesWrapper.GetInternalChoices());
            choicesGrammarBuilder.Culture = this.cultureInfo;
            this.grammarBuilder.Append(choicesGrammarBuilder, minRepeat, maxRepeat);
        }

        void GrammarBuilderWrapper.Append(GrammarBuilderWrapper grammarBuilderWrapper, int minRepeat, int maxRepeat)
        {
            if (this.cultureInfo == null)
            {
                throw new InvalidOperationException("No culture object defined for nested GrammarBuilder");
            }
            this.grammarBuilder.Append((GrammarBuilder)grammarBuilderWrapper.GetInternalGrammarBuilder(), minRepeat, maxRepeat);
        }

        void GrammarBuilderWrapper.Append(string text)
        {
            grammarBuilder.Append(text);
        }

        object GrammarBuilderWrapper.GetInternalGrammarBuilder()
        {
            return grammarBuilder;
        }

        void GrammarBuilderWrapper.SetCulture(CultureInfo cultureInfo)
        {
            grammarBuilder.Culture = cultureInfo;
            this.cultureInfo = cultureInfo;
        }
    }
}
