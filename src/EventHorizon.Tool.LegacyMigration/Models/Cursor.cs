using System;

namespace EventHorizon.Tool.LegacyMigration.Models
{
    public class Cursor
    {
        public string Id { get; set; }
        public bool IsActive { get; set; }
        public bool IsPaused { get; set; }
        public string[] Types { get; set; }
        public DateTime EventDateTime { get; set; }
        public DateTime UpdatedDateTime { get; set; }
    }
}
