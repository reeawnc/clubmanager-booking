using System;
namespace BookingsApi.Models
{
    public class Cell
    {
        public string CssClass { get; set; }
        public string Summary { get; set; }
        public string ToolTip { get; set; }
        public int? BookingID { get; set; }
        public object CheckInData { get; set; }
        public bool? AllowDirectBookingPayments { get; set; }
        public object UID { get; set; }
        public double? Cost { get; set; }
        public bool? ShowPastWarning { get; set; }
        public bool? ShowCancellationWarning { get; set; }
        public int? CourtSlotID { get; set; }
        public string TimeSlot { get; set; }
        public string FullDate { get; set; }
        public int? MinimumPlayers { get; set; }
        public object PlayersToExtend { get; set; }
        public int? LengthInMinutes { get; set; }
        public string CourtCost { get; set; }
        public object CurrencySymbol { get; set; }
        public object CurrencyName { get; set; }
        public string CurrencyDisplay { get; set; }
        public string Player1 { get; set; }
        public string Player2 { get; set; }
        public string Court { get; internal set; }
        public int? CourtID { get; internal set; }
    }
} 