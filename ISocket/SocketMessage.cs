using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISocket
{
    public class SocketMessage
    {
        public int MesageType { get; set; } = -1;

        public string message { get; set; } = null;

        public int stationId { get; set; } = 1;
    }
}
