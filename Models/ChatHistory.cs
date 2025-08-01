// Models/ChatHistory.cs

using SQLite;
using System;
using System.Collections.Generic;

namespace LoQA.Models
{
    // Represents a single message in a conversation
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

        // The entire conversation history is serialized into this JSON string
        public string HistoryJson { get; set; } = "[]";

        public int MessageCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}