// Services/DatabaseService.cs

using LoQA.Models;
using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LoQA.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _connection;

        // Establishes connection and creates the table if it doesn't exist.
        public async Task InitAsync()
        {
            if (_connection != null)
                return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "ChatSessions.db3");
            _connection = new SQLiteAsyncConnection(databasePath);
            await _connection.CreateTableAsync<ChatHistory>();
        }

        // Retrieves a list of all conversations, ordered by the most recently modified.
        public async Task<List<ChatHistory>> ListConversationsAsync()
        {
            await InitAsync();
            return await _connection!.Table<ChatHistory>().OrderByDescending(ch => ch.LastModified).ToListAsync();
        }

        // Loads a single conversation by its unique ID.
        public async Task<ChatHistory?> GetConversationAsync(int id)
        {
            await InitAsync();
            return await _connection!.Table<ChatHistory>().Where(ch => ch.Id == id).FirstOrDefaultAsync();
        }

        // Saves a conversation (either by inserting a new one or updating an existing one).
        public async Task<int> SaveConversationAsync(ChatHistory chat)
        {
            await InitAsync();
            chat.LastModified = DateTime.UtcNow; // Always update the timestamp

            if (chat.Id != 0)
            {
                return await _connection!.UpdateAsync(chat);
            }
            else
            {
                chat.CreatedAt = DateTime.UtcNow;
                return await _connection!.InsertAsync(chat);
            }
        }

        // Deletes a conversation from the database by its ID.
        public async Task<int> DeleteConversationAsync(int id)
        {
            await InitAsync();
            return await _connection!.DeleteAsync<ChatHistory>(id);
        }
    }
}