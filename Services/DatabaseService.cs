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
        // FIX: Make the connection field nullable to handle lazy initialization.
        private SQLiteAsyncConnection? _connection;

        public async Task InitAsync()
        {
            if (_connection != null)
                return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "ChatSessions.db3");
            _connection = new SQLiteAsyncConnection(databasePath);
            await _connection.CreateTableAsync<ChatHistory>();
        }

        public async Task<List<ChatHistory>> ListConversationsAsync()
        {
            await InitAsync();
            return await _connection!.Table<ChatHistory>().OrderByDescending(ch => ch.LastModified).ToListAsync();
        }

        public async Task<ChatHistory?> LoadConversationAsync(int id)
        {
            await InitAsync();
            return await _connection!.Table<ChatHistory>().Where(ch => ch.Id == id).FirstOrDefaultAsync();
        }

        public async Task<int> SaveConversationAsync(ChatHistory chat)
        {
            await InitAsync();
            if (chat.Id != 0)
            {
                return await _connection!.UpdateAsync(chat);
            }
            else
            {
                return await _connection!.InsertAsync(chat);
            }
        }

        public async Task<int> DeleteConversationAsync(int id)
        {
            await InitAsync();
            return await _connection!.DeleteAsync<ChatHistory>(id);
        }
    }
}