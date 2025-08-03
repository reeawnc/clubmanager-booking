using System;
using System.Collections.Generic;
using System.Text;

namespace clubmanager_booking.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Booking
    {
        public int BookingID { get; set; }
        public string DisplayDate { get; set; }
        public string DisplayTime { get; set; }
        public string Court { get; set; }
        public string MatchSummary { get; set; }
        public bool IsEditable { get; set; }
        public bool CanCancelWithRefund { get; set; }
        public bool CanCancel { get; set; }
        public bool IsMovable { get; set; }
        public bool IsCheckInable { get; set; }
        public List<object> Players { get; set; }
        public int MatchType { get; set; }
        public bool CanNotify { get; set; }
        public bool IsPlayer1 { get; set; }
        public int Colspan { get; set; }
        public string CheckInTimestamp { get; set; }
        public string MatchTypeDescription { get; set; }
        public bool IsNoShow { get; set; }
        public string EntryCode { get; set; }
        public bool IsUpdatable { get; set; }
        public string GuestSummary { get; set; }
        public string ResourceSummary { get; set; }
    }

    public class MyBookings
    {
        public List<Booking> Bookings { get; set; }
        public bool UseCourtCredits { get; set; }
        public bool UseBookingsBalance { get; set; }
        public object LastTransaction { get; set; }
        public bool IsLoggedIn { get; set; }
    }


}
