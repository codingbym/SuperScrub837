using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperScrub837.Core837
{
    /// <summary>
    /// This is the class for the header and trailer of the claim file, if applicable
    /// Pretty self-explanatory - added property for batch type too
    /// </summary>
    public class HeadFoot837
    {
        private string header;

        public string Header { get => header; set => header = value; }

        private Tuple<string,char> batchType;

        public Tuple<string,char>BatchType { get => batchType; set => batchType = value; }

        private string footer;

        public string Footer { get => footer; set => footer = value; }
    }
}
