using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using Bodh.ai;
using MySqlConnector;

public interface IMySqlVectorRepository
{
    Task SaveMessageAsync(ChatMessage messageData, string chatId);
    Task UpdateMessageAsync(ChatMessage messageData, string chatId);
    Task<List<string>> SearchSimilarMessagesAsync(float[] queryEmbedding, string chatId, int topK = 3);
    Task SaveChatHistoryAsync(ChatHistory chatHistoryData);
    Task CreateNewChatTableAsync(string chatId);
    Task DeleteChatTableAsync(string chatId);
    Task<List<ChatHistory>> GetChatHistoryAsync();
    Task<List<ChatMessage>> GetChatMessagesAsync(string chatId);
    Task CreateChatHistoryTableIfNotExistsAsync();
    Task DeleteChatHistoryAsync(string chatId);
    Task UpdateChatTitleAsync(string chatId, string newTitle);
    Task CreateEmbeddingTableIfNotExistsAsync();
    Task SaveEmbeddingAsync(EmbeddingModel embeddingData, string chatId);
}

public class MySqlVectorRepository : IMySqlVectorRepository
{
    private readonly string _connectionString;
    private readonly ILoggerService _logger;

    public MySqlVectorRepository(string connectionString, ILoggerService? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger ?? LoggerService.Instance;
    }

    // Developer code: all database access is isolated here so the page-level code stays focused on UI and orchestration.
    public async Task SaveMessageAsync(ChatMessage messageData, string chatId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = $@"
            INSERT INTO `tbl_{chatId}` (MessageId, MessageRole, Content, IsUser, MessageTimestamp)
            VALUES (@MessageId, @MessageRole, @Content, @IsUser, @MessageTimestamp);";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@MessageId", messageData.MessageId);
            cmd.Parameters.AddWithValue("@MessageRole", messageData.Role);
            cmd.Parameters.AddWithValue("@Content", messageData.Content);
            cmd.Parameters.AddWithValue("@IsUser", messageData.IsUser);
            cmd.Parameters.AddWithValue("@MessageTimestamp", messageData.Timestamp);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogEvent($"Saved message for chat {chatId}: {messageData.Role}", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"SaveMessageAsync failed for chat {chatId}");
            throw;
        }
    }

    public async Task UpdateMessageAsync(ChatMessage messageData, string chatId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = $@"
                UPDATE `tbl_{chatId}`
                SET Content = @Content, MessageTimestamp = @MessageTimestamp
                WHERE MessageId = @MessageId;";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@MessageId", messageData.MessageId);
            cmd.Parameters.AddWithValue("@Content", messageData.Content);
            cmd.Parameters.AddWithValue("@MessageTimestamp", messageData.Timestamp);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"UpdateMessageAsync failed for chat {chatId}");
        }
    }

    public async Task<List<string>> SearchSimilarMessagesAsync(float[] queryEmbedding, string chatId, int topK = 3)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"SELECT Content, Embedding FROM tbl_Embeddings WHERE ChatId = @ChatId;";
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@ChatId", chatId);
            using var reader = await cmd.ExecuteReaderAsync();

            var matches = new List<(string Content, float Similarity)>();

            while (await reader.ReadAsync())
            {
                string content = reader.GetString(0);
                byte[] blob = (byte[])reader.GetValue(1);
                float[] storedEmbedding = MemoryMarshal.Cast<byte, float>(blob).ToArray();

                if (storedEmbedding.Length == queryEmbedding.Length)
                {
                    float similarity = TensorPrimitives.CosineSimilarity(queryEmbedding, storedEmbedding);
                    matches.Add((content, similarity));
                }
            }

            return matches
                .OrderByDescending(m => m.Similarity)
                .Take(topK)
                .Select(m => m.Content)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"SearchSimilarMessagesAsync failed for chat {chatId}");
            return new List<string>();
        }
    }

    public async Task SaveChatHistoryAsync(ChatHistory chatHistoryData)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
            INSERT INTO tblHistory (ChatId, ChatTitle, CreatedAt)
            VALUES (@ChatId, @ChatTitle, @CreatedAt);";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@ChatId", chatHistoryData.ChatId);
            cmd.Parameters.AddWithValue("@ChatTitle", chatHistoryData.ChatTitle);
            cmd.Parameters.AddWithValue("@CreatedAt", chatHistoryData.CreatedAt);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogEvent($"Saved chat history for {chatHistoryData.ChatId}: {chatHistoryData.ChatTitle}", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"SaveChatHistoryAsync failed for chat {chatHistoryData.ChatId}");
            throw;
        }
    }

    public async Task CreateNewChatTableAsync(string chatId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = $@"
            CREATE TABLE IF NOT EXISTS `tbl_{chatId}` (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                MessageId VARCHAR(255) NOT NULL,
                MessageRole VARCHAR(50) NOT NULL,
                Content TEXT NOT NULL,
                IsUser BOOLEAN NOT NULL,
                MessageTimestamp DATETIME NOT NULL
            );";

            using var cmd = new MySqlCommand(query, connection);
            await cmd.ExecuteNonQueryAsync();
            _logger.LogEvent($"Created table for chat {chatId}", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"CreateNewChatTableAsync failed for chat {chatId}");
            throw;
        }
    }

    public async Task DeleteChatTableAsync(string chatId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = $@"DROP TABLE IF EXISTS `tbl_{chatId}`;";

            using var cmd = new MySqlCommand(query, connection);
            await cmd.ExecuteNonQueryAsync();
            _logger.LogEvent($"Deleted table for chat {chatId}", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"DeleteChatTableAsync failed for chat {chatId}");
        }
    }

    public async Task<List<ChatHistory>> GetChatHistoryAsync()
    {
        var chatSessions = new List<ChatHistory>();
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"SELECT ChatId, ChatTitle, CreatedAt FROM tblHistory ORDER BY CreatedAt DESC;";
            using var cmd = new MySqlCommand(query, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                chatSessions.Add(new ChatHistory
                {
                    ChatId = reader.GetString(0),
                    ChatTitle = reader.GetString(1),
                    CreatedAt = reader.GetDateTime(2)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "GetChatHistoryAsync failed");
            return new List<ChatHistory>();
        }

        return chatSessions;
    }

    public async Task<List<ChatMessage>> GetChatMessagesAsync(string chatId)
    {
        var messages = new List<ChatMessage>();
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = $@"SELECT MessageId, MessageRole, Content, IsUser, MessageTimestamp FROM `tbl_{chatId}` ORDER BY MessageTimestamp ASC;";
            using var cmd = new MySqlCommand(query, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                messages.Add(new ChatMessage
                {
                    MessageId = reader.GetString(0),
                    Role = reader.GetString(1),
                    Content = reader.GetString(2),
                    IsUser = reader.GetBoolean(3),
                    Timestamp = reader.GetDateTime(4)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"GetChatMessagesAsync failed for chat {chatId}");
            return new List<ChatMessage>();
        }

        return messages;
    }

    public async Task CreateChatHistoryTableIfNotExistsAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
            CREATE TABLE IF NOT EXISTS tblHistory (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                ChatId VARCHAR(255) NOT NULL,
                ChatTitle VARCHAR(255) NOT NULL,
                CreatedAt DATETIME NOT NULL
            );";

            using var cmd = new MySqlCommand(query, connection);
            await cmd.ExecuteNonQueryAsync();
            _logger.LogEvent("Ensured tblHistory exists", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "CreateChatHistoryTableIfNotExistsAsync failed");
            throw;
        }
    }

    public async Task DeleteChatHistoryAsync(string chatId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string historyQuery = @"DELETE FROM tblHistory WHERE ChatId = @ChatId;";
            using (var historyCmd = new MySqlCommand(historyQuery, connection))
            {
                historyCmd.Parameters.AddWithValue("@ChatId", chatId);
                await historyCmd.ExecuteNonQueryAsync();
            }

            // Agent code: delete embedding rows for the same chat so stale vectors do not affect future retrieval.
            string embeddingQuery = @"DELETE FROM tbl_Embeddings WHERE ChatId = @ChatId;";
            using (var embeddingCmd = new MySqlCommand(embeddingQuery, connection))
            {
                embeddingCmd.Parameters.AddWithValue("@ChatId", chatId);
                await embeddingCmd.ExecuteNonQueryAsync();
            }

            _logger.LogEvent($"Deleted chat history and embeddings for chat {chatId}", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"DeleteChatHistoryAsync failed for chat {chatId}");
        }
    }

    public async Task UpdateChatTitleAsync(string chatId, string newTitle)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"UPDATE tblHistory SET ChatTitle = @NewTitle WHERE ChatId = @ChatId;";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@NewTitle", newTitle);
            cmd.Parameters.AddWithValue("@ChatId", chatId);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogEvent($"Renamed chat {chatId} to {newTitle}", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"UpdateChatTitleAsync failed for chat {chatId}");
        }
    }

    public async Task CreateEmbeddingTableIfNotExistsAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS tbl_Embeddings (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                ChatId VARCHAR(255) NOT NULL,
                Content TEXT NOT NULL,
                Embedding BLOB NOT NULL
            );";

            using (var cmd = new MySqlCommand(createTableQuery, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Developer code: this keeps older databases safe by adding the chat grouping column on first launch.
            try
            {
                string alterQuery = @"ALTER TABLE tbl_Embeddings ADD COLUMN ChatId VARCHAR(255) NULL;";
                using var alterCmd = new MySqlCommand(alterQuery, connection);
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex) when (ex.Number == 1060)
            {
                // Duplicate column is expected if the migration already ran.
            }

            _logger.LogEvent("Ensured tbl_Embeddings schema exists", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "CreateEmbeddingTableIfNotExistsAsync failed");
            throw;
        }
    }

    public async Task SaveEmbeddingAsync(EmbeddingModel embeddingData, string chatId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            byte[] embeddingBytes = MemoryMarshal.AsBytes(embeddingData.Embedding.AsSpan()).ToArray();

            string query = @"
            INSERT INTO tbl_Embeddings (ChatId, Content, Embedding)
            VALUES (@ChatId, @Content, @Embedding);";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@ChatId", chatId);
            cmd.Parameters.AddWithValue("@Content", embeddingData.Content);
            cmd.Parameters.AddWithValue("@Embedding", embeddingBytes);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogEvent($"Saved embedding for chat {chatId}: {embeddingData.Content.Substring(0, Math.Min(embeddingData.Content.Length, 80))}", "DATABASE");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"SaveEmbeddingAsync failed for chat {chatId}");
            throw;
        }
    }
}