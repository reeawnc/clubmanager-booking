using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace clubmanager_booking.Models
{
    public class UserMessage
    {
        public List<MessageResponseAction> MessageResponseActions { get; set; }
        public int MessageID { get; set; }
        public string MessageSubject { get; set; }
        public string MessageText { get; set; }
        public string Actions { get; set; }
        public string StartDate { get; set; }
        public bool IsExpired { get; set; }                
    }
}
