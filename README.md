# local-rag-workshop

A collection of dotnet projects presented as a learning tool for becoming familiar with RAG
workflows. How they function under the hood and how to get one running in a strictly local environment.
The original intent of this repo is to be used in a workshop type of environment and as such the 

All projects are under the `src/` folder, each having a varying level of completeness in the goal of
providing as much or as little guidance as is desired.

> Disclaimer: I am not and will not pretend to be an expert in this space. Statements in this workshop
> are made as accurate to my knowledge as possible. 

# First Time Set Up

Before running any of the projects, there's some initial set up required. If you run into any issues that can't be resolved you can try
doing a full reset by following the directions under [#Final-Clean-Up] and restarting this section again.

To perform the initial set up you'll want to ensure the containers are running by executing the following command on the terminal:

```bash
docker compose up -d 
# If you run into an error, make sure the Docker Engine is running!
```

This will read from the `docker-compose.yml` file to stand up the containers specified within, passing the `-d` flag just runs it in detached mode so your terminal remains free after.

After the previous command completes, there are a couple of tasks that need to be done.
- Enable the pgvector extension in the Postgres container.
- Pull the desired chat and embeddings models in the Ollama container.

*Important note:*
If for any reason you end up having issues and/or need to do a fresh start you can run `docker compose down` followed by `docker volume rm ollama_data postgresql_data`
The first will shutdown and delete the containers, the second will delete the persistent data store created for those containers.


__Enable the pgvector extension__

The pgvector extension is the addon for Postgres which enables vector storage and querying. To do this the following commands need to be run:

First launch the psql executable on the postgres container:
```bash
# On the 'postgresql' container, execute the 'psql' executable with arguments '-U admin' (standing for User admin) 
docker exec -it postgresql ./bin/psql -U admin

# With the updated terminal, you should see the line beginning with 'admin=#'
CREATE EXTENSION IF NOT EXISTS vector;
SELECT extname FROM pg_extension;

# Exit the container
exit
```

__Pull the desired chat and embeddings models__

We need to pull down the models we want to utilize before we attempt to use them. To do this we need to enter
the ollama container and pull them down. Run the following commands:

```bash
# Open a bash shell on the container, afterwards you should see the line beginning with 'root@<CONTAINER_ID>:/#'
docker exec -it ollama bash

# Pull the embeddings model (currently most popular one on ollama.com)
ollama pull nomic-embed-text

# Pull the chat model (A lightweight open model from Microsoft)
ollama pull phi3:latest

# Exit the container
exit
```

# Final Clean Up

All changes to the code and repo can be cleared up by running `git reset --hard main` which will return you to the initial state of the `main` branch.

__Reset the state of the repository__

You can reset your local repo to the starting point by running the following command:
```bash
git reset --hard main
```

__Tear down containers__

One of the following commands, based on your discretion, can be used to clean up the docker set up.

```bash
# Tear down and delete the containers
docker compose down

# Do all of the above but also wipe the volumes (where the sql data and ollama models are stored)
docker compose down --volumes

# Do a full clean (containers, images, and volumes)
docker compose down --rmi all --volumes
```

# Rough Overview on RAG

At a high-level, the goal of these projects will be meeting the following requirements to stand up
our basic RAG workflow.

I. Initialize and connect to our services
---

For this we simply want to make sure we set up whatever storage, chat, and embeddings client we'll be using. 

For these workshops it'll be:
- **Postgresql** for storage and querying
- **Ollama** for embeddings and chat completions

II. Ingest the data and create the search index
---

We want to make our input data searchable based on its 
"lexical adjacency" (aka a term I just made up). In other words we want
to have some way of mapping the user's query to relevant documents in
our database. This mapping happens via our embedding model.

For every value we want to...
1. Read in the value
2. Store it in our database
3. Generate an embedding for our model (AKA map it to an n-dimensional vector space)
4. Store that embedding along with a reference to our original row in a vector table in our database.

III. Use the user's query to find the relevant data from our input
---
This is where the ✨magic✨ happens. 

We can use the embedding model to map our user's query to the same n-dimensional vector space we mapped our values to. From there, 
we can use our desired search algorithm, KNN for example, to find the values "closest" to our user's query. 
The resulting values can then be fed into our chat completion client to provide the LLM with additional data to reference as it generates a response.
(Although you don't even have to go that far, it's pretty effective as just a basic search mechanism for things like onboarding docs.)

> Disclaimer what follows is probably a huge oversimplification fraught with errors.

Under the hood, this works because it's basically what LLMs are already doing when they do things like chat completion. Given some text, they
try to determine what the next most likely word is. They do this by just tokenizing the user's text (and their response so far), mapping it into
a vector space, and retrieving what the most next most likely token is to build the response. 

Alternatively, in simple terms because I'm a simple person, it does math to find things that sound similar to what its already heard.

Functionally, the things that happen during this stage are:
1. Read in the user's query.
2. Convert the query into a set of embeddings (AKA map in to our n-dimensional vector space).
3. Perform a vector search on our vector table to find similar embeddings.
4. From the similar embeddings, retrieve the original values from our database.

IV. "Augment" the chat completion with related values
---
After the previous section, this one is quite brief. With the values that are similar to
the user's query, we feed those into the chat completion client (typically as something
like a system message) along with the user's query, and return the response.

## Further considerations and improvements

This is an incredibly basic and rudimentary example of a RAG workflow and while honestly useful in its own right, there are some considerations
and improvements that should be made before your applying your RAG workflow to everything.

**Some things to think about:**

*Do you need to final auto-complete step or would it be enough to simply return the list of values back to the user?*

Maybe you or your users don't care for the final response message and would benefit primarily just from the semantic search. Chat models are 
significantly more expensive computationally compared to embedding models. If you don't really need that final message, cut it. Save the compute,
complexity and avoid the risk of hallucinations. It'll run better locally and/or leave you with less operational costs.

*Is it easier to store the values directly with the embeddings?*

This workshop separates the two out into a vector search table and a traditional data table. That isn't necessarily essential and is done primarily
to allow for different indexes or search methods to be created on the same data. If you don't need the extra complexity of multiple tables, just 
group them together.

*In a complex data model, what values are worth embedding?*

Crude examples of RAG will just embed the entire value. However, that may not be ideal as you may only want to search on a part of a complex data model.
Additionally, you may want to have multiple embeddings to allow for a more targeted search if your data model has a wide variety of data points. Unfortunately,
I can't provide a quick and easy answer for what to embed as it's going to depend heavily on __what your users want__ and __what your data looks like__. If
you're building a catalog of personal notes for easy searching, maybe you just want to pull headings? Maybe there are images, graphs and other visuals that
you want to separate into another vector table, etc.

*What is the length of the data you're embedding?*

There are two upper limits when it comes to the length of data that you create embeddings for. One, there's a token limit that your model can't exceed, plus more tokens are more expensive.
Two, there's a sweet spot of having the right amount of data in an embedding that your search is relevant. Too little data and the search results are wildly inaccurate, you may return
values that have nothing to do with the users query. Too much data and your search results (in theory) start to become too general and if you're feeding those results into another chat client
directly, you blow your token budget.

*Adding intermediate steps.*

At every stage, especially in a from scratch set up like this, you can add customizations and tweaks. I've not tested all of these personally, but 
some examples I've seen, done, or would be curious to try are: 
- Adding an "Intent" or "Summary" chat client to summarize the user's query into a shorter or more accurate form for your data.
- Using a chat client to summarize the data you're creating the index of with the goal of minimizing the size of that data.
- Use an antagonistic client to compare the resulting data against the user's query and estimate if it's relevant or if a new search should be done.
- Split the input data set into small more relevant chunks such as headings for text/markdown documents.
- Perform two searches in serial, for example one to find a relevant chapter in a book and another to find relevant contents in that chapter.

*Using different embedding models.*

I personally don't have the experience to know of the intricacies between different embeddings models and their performance. However, you should absolutely
feel free and feel encouraged to experiment with different models to see if one works better for your use case than another. If you do, please reach out and
let me know of your findings. I'm also terribly curious to know how well the embeddings from one model would line up to embeddings from another model.

# FAQ

*Do you have to use Docker for hosting our database and models?*

No, use whatever you like, are comfortable with, or your team prefers. Docker was chosen for this project simply for portability and ease of use.

*Why does this use Ollama?*

It had a good convenient Docker image, decent interfaces in dotnet, is __free__ and has pretty decent documentation.

*Why does this use PostgreSQL*

For a very similar reason as why Ollama. Good docker image, good docs, free. Most importantly though it had all of these qualities along with
having support for vector search and vector tables. I've also found success using Redis for this on other projects but for a learning experience
being able to use SQL, a much more commonly known and used language, I chose PostgreSQL. (Plus it was a good excuse to learn it a bit better.)
