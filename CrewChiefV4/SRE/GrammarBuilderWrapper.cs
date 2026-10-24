using System;
using System.Globalization;

namespace CrewChiefV4.SRE
{
    interface GrammarBuilderWrapper
    {
        void SetCulture(CultureInfo cultureInfo); // internal.Culture = ...

        void Append(ChoicesWrapper choicesWrapper);

        void Append(ChoicesWrapper choicesWrapper, int minRepeat, int maxRepeat);

        void Append(GrammarBuilderWrapper grammarBuilderWrapper, int minRepeat, int maxRepeat);

        void Append(String text);

        object GetInternalGrammarBuilder();
    }
}
