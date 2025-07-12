using System.Drawing;
using Npgsql;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using Pgvector;

namespace LocalRagCli;

class Program
{
    /**
    * A basic implementation of a RAG chat workflow. There are a number of improvements which should be made
    * but this provides a non-looping hopefully simplified implementation.
    **/
    static string POSTGRES_CONN_STRING = "Host=localhost;Port=5432;Database=postgres;Username=admin;Password=Password123!";
    static string OLLAMA_URI = "http://localhost:11434";
    static string CHAT_MODEL = "phi3:latest";
    static string EMBEDDINGS_MODEL = "nomic-embed-text";
    static int EMBEDDINGS_DIMENSION = 768; // This value will be determined by your model of choice for nomic-embed-text it's 768

    static async Task Main(string[] args)
    {
        // Step 1. Initialize the storage connection, chat client, and embedding client
        Console.WriteLine("Initializing...");
        var builder = new NpgsqlDataSourceBuilder(POSTGRES_CONN_STRING);
        builder.UseVector();
        var db = builder.Build();

        await using var dbConn = await db.OpenConnectionAsync();
        var ollama = new OllamaApiClient(OLLAMA_URI);

        Console.WriteLine("Ensuring pgvector extension is enabled...");
        await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public"))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine("Creating table...");
        // Step 1a. Initialize the tables in the database.
        await using (var cmd = new NpgsqlCommand(
            $"CREATE TABLE if not exists data ({nameof(DataRow.Id)} serial PRIMARY KEY, {nameof(DataRow.Value)} VARCHAR(256) NOT NULL, {nameof(DataRow.Embedding)} VECTOR({EMBEDDINGS_DIMENSION}));",
            dbConn
        ))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine("Initialized.");

        // Step 2. Loop over and process the data set
        Console.WriteLine("Inserting Values...");
        var exampleValues = new List<string>()
        {
            "Juzbo is a goblin booyahg that has turned into a party NPC much to the DMs annoyance",
            "Big Honker is a T-rex and the party's mascot. He is present on all of the party's armor pieces",
            "Chal C'pyryte is a copper Dragonborn fighter in the party and has something like 80 grandkids",
            "Nixie Vetra, real name Brigitte Wahls, is the party's Human Wizard and also the one that adopted Juzbo",
            "Saeth, short for Saethaleil Nailo, is the himbo Elf Bard of the party inspired by Aragorn from Lord of the Rings",
            "Oscar Seabringer is the Human Ranger of the party, he's known for catching fish by tickling them.",
            "Cleric-Kun is the Half-Elf Cleric of the party and is played by a first timer. He tends to just shoot arrows."
        };

        for (int i = 0; i < exampleValues.Count(); i++)
        {
            // Step 2a. Read in a value
            var id = i;
            var val = exampleValues[i];

            // Step 2b. Generate the embeddings for the value
            var valEmbedRequest = new EmbedRequest()
            {
                Model = EMBEDDINGS_MODEL, // You could specify a default model, choosing explicit here for workshop clarity
                Input = [val]
            };

            var embeddingsResponse = await ollama.EmbedAsync(valEmbedRequest);
            var embedding = embeddingsResponse.Embeddings[0]; // Multiple values can be passed to the Embed Async method, we only passed one.

            // Step 2c. Insert the value into the database
            await using var dataInsertCmd = new NpgsqlCommand($"INSERT INTO data({nameof(DataRow.Id)}, {nameof(DataRow.Value)}, {nameof(DataRow.Embedding)}) VALUES ($1, $2, $3)", dbConn)
            {
                Parameters =
                {
                    new() { Value = i },
                    new() { Value = val },
                    new() { Value = embedding }
                }
            };

            await dataInsertCmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine("Values inserted.");
        Console.WriteLine();

        // Step 3. Read the user's query
        Console.WriteLine("Please enter your question below...");
        var userQuery = Console.ReadLine() ?? "";

        // Step 4. Generate an embedding for the user's query.
        var userEmbedRequest = new EmbedRequest()
        {
            Model = EMBEDDINGS_MODEL, // You could specify a default model, choosing explicit here for workshop clarity
            Input = [userQuery],
        };

        var userEmbeddingsResponse = await ollama.EmbedAsync(userEmbedRequest);
        var userEmbedding = userEmbeddingsResponse.Embeddings[0];

        // Step 5. Perform a vector search for adjacent embeddings in the database.
        await using var userSearchCmd = new NpgsqlCommand($"SELECT * FROM data ORDER BY {nameof(DataRow.Embedding)} <-> $1 LIMIT 5", dbConn)
        {
            Parameters = {
                new () { Value = new Vector(userEmbedding) }
            }
        };

        var searchResults = new List<DataRow>();
        await using (var userSearchReader = await userSearchCmd.ExecuteReaderAsync())
        {
            while (await userSearchReader.ReadAsync())
            {
                int id = (int)userSearchReader[nameof(DataRow.Id)];
                string value = (string)userSearchReader[nameof(DataRow.Value)];
                Vector embedding = (Vector)userSearchReader[nameof(DataRow.Embedding)];

                searchResults.Add(new(id, value, embedding.ToArray()));
            }
        }

        // Step 6. Pass the results along with the user prompt into the chat completion client
        var chatMessages = new List<Message>();

        chatMessages.Add(new(ChatRole.System, "You are a search agent helping answer the user's query, you will receive text related to the user's query and should use those to answer."));
        foreach (var result in searchResults)
        {
            chatMessages.Add(new(ChatRole.Assistant, result.Value));
        }
        chatMessages.Add(new(ChatRole.User, userQuery));
        var chatRequest = new ChatRequest()
        {
            Model = CHAT_MODEL, // You could specify a default model, choosing explicit here for workshop clarity
            Messages = chatMessages,

        };

        // Step 7. Output the full response.
        await foreach (var answerToken in ollama.ChatAsync(chatRequest))
        {
            Console.Write(answerToken?.Message.Content);
        }

        // Step 8. (Optional) Loop back to Step 3

        // Step 9. (Optional) Drop the tables in the database to avoid duped data on consecutive runs
        await using var dropTableCmd = new NpgsqlCommand("DROP TABLE data", dbConn);

        await dropTableCmd.ExecuteNonQueryAsync();
    }

    public record DataRow(int Id, string Value, float[] Embedding);

}
