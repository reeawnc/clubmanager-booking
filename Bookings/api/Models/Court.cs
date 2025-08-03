using System;
using System.Collections.Generic;

namespace BookingsApi.Models
{
    public class Court
    {
        public List<Cell> Cells { get; set; }
        public string ColumnHeading { get; set; }
        public int? CourtID { get; set; }
        public object AssociatedCourtID { get; set; }
        public string CssClass { get; set; }
        public string EarliestStartTime { get; set; }
    }
} 