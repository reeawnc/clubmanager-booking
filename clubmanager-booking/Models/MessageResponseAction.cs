using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace clubmanager_booking.Models
{
    public class MessageResponseAction
    {
        public string PlayerName { get; set; }
        public string ActionType { get; set; }
        public string ResponseCssClass { get; set; }
        public string ActionDateTime { get; set; }
        public int PlayerID { get; set; }
    }
}
