using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsterNET.ARI.Proxy.APCoR.Models
{
    public class DialogueModel
    {
        public string Id { get; set; }
        public string Application { get; set; }
        public DateTime Created { get; set; }
        public int MsgCount { get; set; }
    }
}
