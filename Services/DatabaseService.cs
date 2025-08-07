using LoQA.Models;
using SQLite;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace LoQA.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _connection;

        public async Task InitAsync()
        {
            if (_connection != null)
                return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "ChatSessions.db3");
            Debug.WriteLine($"DATABASE PATH: {databasePath}");

            _connection = new SQLiteAsyncConnection(databasePath);
            await _connection.CreateTableAsync<ChatHistory>();
            await _connection.CreateTableAsync<LlmModel>();
        }

        public async Task<List<ChatHistory>> ListConversationsAsync()
        {
            await InitAsync();
            return await _connection!.Table<ChatHistory>().OrderByDescending(ch => ch.LastModified).ToListAsync();
        }

        public async Task<int> SaveConversationAsync(ChatHistory chat)
        {
            await InitAsync();
            chat.LastModified = DateTime.UtcNow;

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

        public async Task<int> DeleteConversationAsync(int id)
        {
            await InitAsync();
            return await _connection!.DeleteAsync<ChatHistory>(id);
        }

        public async Task<List<LlmModel>> GetModelsAsync()
        {
            await InitAsync();
            return await _connection!.Table<LlmModel>().ToListAsync();
        }

        public async Task<int> SaveModelAsync(LlmModel model)
        {
            await InitAsync();
            if (model.Id != 0)
            {
                return await _connection!.UpdateAsync(model);
            }
            else
            {
                return await _connection!.InsertAsync(model);
            }
        }

        public async Task<int> DeleteModelAsync(int id)
        {
            await InitAsync();
            return await _connection!.DeleteAsync<LlmModel>(id);
        }
    }
}