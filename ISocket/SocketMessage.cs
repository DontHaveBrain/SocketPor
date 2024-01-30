using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISocket
{
    public class SocketMessage
    {
        public int MesageType { get; set; }

        public string message { get; set; }

        public int stationId { get; set; }
    }
}
