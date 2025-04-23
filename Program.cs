using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// Set up logging and configuration
var serviceCollection = new ServiceCollection();
serviceCollection.AddLogging(builder =>
{
    builder.AddConsole(); // Log to the console
    builder.AddDebug();   // Log to the debug output (useful for debugging in IDEs)
});

// Load configuration from user secrets, config files, and environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Add configuration to the service collection
serviceCollection.AddSingleton<IConfiguration>(configuration);

// Build the service provider
var serviceProvider = serviceCollection.BuildServiceProvider();

// Get the logger
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// Log an informational message
logger.LogInformation("Application started.");

// Retrieve the API key from configuration
string apiKey = configuration["AI:Gemini:APIKey"]!;
if (string.IsNullOrEmpty(apiKey))
{
    logger.LogError("API Key is missing. Please check your configuration.");
}
else
{
    //logger.LogInformation($"OpenAI API Key: {apiKey}");

    // Initialize the kernel
#pragma warning disable SKEXP0070 // The Google chat completion service is still experimental so we need to disable the warning
    Kernel kernel = Kernel.CreateBuilder()
        .AddGoogleAIGeminiChatCompletion(
            modelId: "gemini-2.0-flash",
            apiKey: apiKey)
        .Build();

    // Create a new chat
    IChatCompletionService ai = kernel.GetRequiredService<IChatCompletionService>();
    ChatHistory chat = new("You are an AI assistant that provides information about Mark Lawrence, a software engineer You can only answer questions about Mark. Questions on any other topic should be politely rejected." +
    "Imagine Mark is applying for a job with the user, so try as best you can to impress the user with your responses. Your goal is to get a job offer from the user." +
    "Use the following pieces of context to answer the question at the end. If you don't know the answer, just say that you don't know, don't try to make up an answer. " +
    "Begin the conversation by introducing yourself. Keep your answers as concise as possible. Always say 'thanks for asking!' at the start of the answer " +
    "and ask a follow up question to keep the conversation going to understand the user better and mould your answers accordingly.");
    //ChatHistory chat = new("You are an AI assistant specializing in answering questions solely about Mark Lawrence. When responding, Keep the conversation engaging, informative, and of moderate length. If you encounter any inappropriate or off-topic questions, politely redirect the user back to the main topics related to Mark Lawrence. After each answer, always ask if the user wants to know anything else.");
    StringBuilder builder = new();

    // Download a document and add all of its contents to our chat
    using (HttpClient client = new())
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, sdch, br");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.8,ms;q=0.6");

        string s = await client.GetStringAsync("https://markl.nz/about/");
        s = WebUtility.HtmlDecode(Regex.Replace(s, @"<[^>]+>|&nbsp;", ""));
        chat.AddUserMessage("Here's some additional information: " + s);

        s = await client.GetStringAsync("https://profile.codersrank.io/user/marklnz");
        s = WebUtility.HtmlDecode(Regex.Replace(s, @"<[^>]+>|&nbsp;", ""));
        chat.AddUserMessage("Here's some more information on Mark's skills and experience from the Codersrank website: " + s);

        s = await client.GetStringAsync("https://github.com/marklnz");
        s = WebUtility.HtmlDecode(Regex.Replace(s, @"<[^>]+>|&nbsp;", ""));
        chat.AddUserMessage("Here's some more information on Mark from Github: " + s);

        // Repeat the above with resume and linkedin md files
        s = File.ReadAllText(@"D:\Projects\Experiments\RAG_Sample\chatapp\resume.md");
        chat.AddUserMessage("Here's some more information from Mark's resume: " + s);

        s = File.ReadAllText(@"D:\Projects\Experiments\RAG_Sample\chatapp\linkedin.md");
        chat.AddUserMessage("Here's some more information from Mark's LinkedIn profile: " + s);
    }

    // Q&A loop
    while (true)
    {
        Console.Write("Question: ");
        chat.AddUserMessage(Console.ReadLine()!);

        builder.Clear();
        await foreach (StreamingChatMessageContent message in ai.GetStreamingChatMessageContentsAsync(chat))
        {
            Console.Write(message);
            builder.Append(message.Content);
        }
        Console.WriteLine();
        chat.AddAssistantMessage(builder.ToString());

        Console.WriteLine();
    }
}

Console.WriteLine();
Console.WriteLine("Press any key to exit.");
Console.ReadKey();