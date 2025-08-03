using ClubManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace clubmanager_booking.Models
{
    public class Messages
    {
        public bool UserHasMessages { get; set; }
        public List<UserMessage> UserMessages { get; set; }
    }
}
