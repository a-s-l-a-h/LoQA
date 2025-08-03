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

        // Establishes connection and creates tables if they don't exist.
        public async Task InitAsync()
        {
            if (_connection != null)
                return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "ChatSessions.db3");
            _connection = new SQLiteAsyncConnection(databasePath);
            await _connection.CreateTableAsync<ChatHistory>();
            // Add this line to create the model table
            await _connection.CreateTableAsync<LlmModel>();
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

        // --- NEW METHODS FOR LLMMODEL ---

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

        public async Task SetActiveModelAsync(int modelId)
        {
            await InitAsync();
            // Deactivate all other models
            await _connection!.ExecuteAsync("UPDATE llm_models SET IsActive = 0");
            // Activate the selected model
            await _connection!.ExecuteAsync("UPDATE llm_models SET IsActive = 1 WHERE Id = ?", modelId);
        }
    }
}