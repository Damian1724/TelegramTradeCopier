using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaQuotesSample
{
    class Message
    {
        public int message_id { get; set; }
        public int forward_from_message_id { get; set; }
        public string caption { get; set; }
        public string text { get; set; }
    }
}
