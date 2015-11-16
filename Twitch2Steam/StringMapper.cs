using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Twitch2Steam
{
    public class StringMapper
    {
        private readonly Dictionary<String, String> data;
        private readonly Regex regex;

        public StringMapper(Dictionary<String, String> input)
        {
            //copy the dictionary so it can't be modified. Only a shallow copy but Strings are immutable anyway
            this.data = new Dictionary<String, String>(input);

            //Desired result is something like "(?<=\\s|^)((Kappa)|(FailFish))(?=\\s|$)"
            StringBuilder regexBuilder = new StringBuilder();
            //In order to ensure to not match Kappa to Kappa! or KappaRoss we need to ensure that before and after the match
            //is either a whitespace (\s) or a start (^) / end ($) of line.
            //However we do not want to capture the whitespaces since it would complicate our dictionary lookups:
            //http://stackoverflow.com/questions/3926451/how-to-match-but-not-capture-part-of-a-regex
            regexBuilder.Append(@"(?<=\s|^)(");
            foreach (var entry in input.Keys)
            {
                regexBuilder.Append("(");
                regexBuilder.Append(entry);
                regexBuilder.Append(")|");
            }

            if(!input.IsEmpty())
                regexBuilder.Remove(regexBuilder.Length - 1, 1);
            regexBuilder.Append(@")(?=\s|$)");
            regex = new Regex(regexBuilder.ToString(), RegexOptions.Compiled);
        }

        public String Map(String input)
        {
            if (data.IsEmpty())
                return input;

            String ret = regex.Replace(input, match =>
            {
                return data[match.Value];
            });

            return ret;
        }
    }
}
