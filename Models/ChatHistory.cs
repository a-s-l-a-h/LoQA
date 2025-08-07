using SQLite;
using System;
using System.Collections.Generic;

namespace LoQA.Models
{
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    [Table("conversations")]
    public class ChatHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = "New Chat";
        public string HistoryJson { get; set; } = "[]";
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}