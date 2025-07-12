namespace LocalRagCli;

class Program
{
    /**
    * The skeleton of a basic RAG workflow implementation. This is intended to provide a semi clean
    * working space for workshops or experimentation. Comments are included here to break down
    * the overall steps and provide guidance on building a basic RAG workflow.
    **/
    static void Main(string[] args)
    {
        // Step 1. Initialize the storage connection, chat client, and embedding client

        // Step 1a. Initialize the tables in the database.

        // Step 2. Loop over and process the data set

            // Step 2a. Read in a value

            // Step 2b. Generate the embeddings for the value

            // Step 2c. Store the row in the database

        // Step 3. Read the user's query

        // Step 4. Generate an embedding for the user's query.

        // Step 5. Perform a vector search for adjacent embeddings in the database.

        // Step 6. Pass these values along with the user prompt into the chat completion client

        // Step 7. Output the full response.

        // Step 8. (Optional) Loop back to Step 3

        // Step 9. (Optional) Drop the tables in the database to avoid duped data on consecutive runs
    }
}
