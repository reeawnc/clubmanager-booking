using System;
using System.Collections.Generic;

namespace BookingsApi.Models
{
    public class RootObject
    {
        public int ProposedOverallWidth { get; set; }
        public int ProposedOverallGridContainerWidth { get; set; }
        public bool ScrollingRequired { get; set; }
        public List<Court> Courts { get; set; }
        public string CalendarDate { get; set; }
        public int NumberOfCourts { get; set; }
        public bool IsDeepLinkBooking { get; set; }
        public int DeepCourtID { get; set; }
        public int DeepCourtSlotID { get; set; }
        public object DeepWhenInfo { get; set; }
        public object DeepTimeSlot { get; set; }
        public object DeepLocation { get; set; }
        public object DeepCourtCost { get; set; }
        public object DailyRecurrencesContainer { get; set; }
        public int AllocationSchedules { get; set; }
        public int ActiveSchedules { get; set; }
        public bool UseOptimalDisplay { get; set; }
        public string AllowDirectBookingPayments { get; set; }
        public string BookingPackagesJson { get; set; }
        public string SlotPackageRestrictions { get; set; }
        public object PackageLinkText { get; set; }
        public double PlayerBookingsBalance { get; set; }
        public double PlayerBookingsCredits { get; set; }
        public object Currency { get; set; }
        public object CurrencySymbol { get; set; }
    }
} 