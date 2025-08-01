// Models/ChatHistory.cs

using SQLite;
using System;

namespace LoQA.Models
{
    [Table("conversations")]
    public class ChatHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // FIX: Initialize to string.Empty to satisfy the non-nullable requirement.
        [NotNull, Collation("NOCASE")]
        [Unique]
        public string Name { get; set; } = string.Empty;

        // FIX: Initialize to an empty JSON array string.
        public string HistoryJson { get; set; } = "[]";

        public int MessageCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastModified { get; set; }
    }
}