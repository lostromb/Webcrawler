using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class MultiRegex
    {
        private readonly IList<Regex> _regexes;

        public MultiRegex()
        {
            _regexes = new List<Regex>();
        }

        public void AddRegex(string regex)
        {
            AddRegex(new Regex(regex));
        }

        public void AddRegex(Regex regex)
        {
            _regexes.Add(regex);
        }

        public Match Match(string input)
        {
            Match returnVal = System.Text.RegularExpressions.Match.Empty;
            foreach (Regex r in _regexes)
            {
                returnVal = r.Match(input);
                if (returnVal.Success)
                {
                    return returnVal;
                }
            }

            return returnVal;
        }
    }
}
