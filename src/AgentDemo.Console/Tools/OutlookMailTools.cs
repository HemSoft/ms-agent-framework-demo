// <copyright file="OutlookMailTools.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace AgentDemo.Console.Tools;

using System.ComponentModel;
using System.Globalization;
using System.Text;

using Azure.Core;
using Azure.Identity;

using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

/// <summary>
/// Provides Outlook/Hotmail mail tools for the AI agent using Microsoft Graph.
/// </summary>
internal static class OutlookMailTools
{
    private const string ClientIdEnvVar = "GRAPH_CLIENT_ID";
    private const string TenantIdEnvVar = "GRAPH_TENANT_ID";
    private const string AuthError = "authentication";
    private const int DefaultMaxResults = 10;
    private const string FolderInbox = "inbox";
    private const string FolderJunk = "junkemail";
    private const string FolderSent = "sentitems";
    private const string FolderDrafts = "drafts";
    private const string FolderDeleted = "deleteditems";
    private const string FolderArchive = "archive";

    private static readonly string[] Scopes = ["User.Read", "Mail.Read", "Mail.ReadWrite", "Mail.Send"];

    private static readonly string AuthRecordPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AgentDemo",
        "graph_auth_record.json");

    private static GraphServiceClient? graphClient;

    /// <summary>
    /// Accesses Outlook/Hotmail mailbox. Supports personal Microsoft accounts (hotmail.com, outlook.com).
    /// </summary>
    /// <param name="mode">Operation: 'inbox', 'spam', 'folder', 'read', 'send', 'search', 'delete', 'move', 'junk'.</param>
    /// <param name="param1">Context-dependent: message ID for read/delete/move/junk, recipient for send, query for search, folder name for 'folder'.</param>
    /// <param name="param2">For 'send': subject. For 'move': destination folder (inbox, archive, deleteditems, junkemail).</param>
    /// <param name="param3">For 'send': body.</param>
    /// <param name="maxResults">Max results for inbox/search (default: 10).</param>
    /// <returns>Result message or error.</returns>
    [Description("Access Outlook/Hotmail mailbox. Modes: 'inbox' (list inbox), 'spam' (list junk folder), 'folder' (list by name), 'read' (id), 'send' (to,subject,body), 'search' (query), 'delete' (id), 'move' (id,folder), 'junk' (mark as junk). Requires GRAPH_CLIENT_ID.")]
    public static async Task<string> MailAsync(
        string mode,
        string? param1 = null,
        string? param2 = null,
        string? param3 = null,
        int maxResults = DefaultMaxResults)
    {
        System.Console.WriteLine($"[Tool] OutlookMail: {mode}");

        var client = GetOrCreateClient();
        if (client is null)
        {
            return $"Error: Set {ClientIdEnvVar} environment variable. Register app at https://entra.microsoft.com with 'Personal Microsoft accounts' support.";
        }

        maxResults = Math.Clamp(maxResults, 1, 50);

        return mode?.ToUpperInvariant() switch
        {
            "INBOX" => await ListFolderAsync(client, FolderInbox, maxResults).ConfigureAwait(false),
            "SPAM" or "JUNKMAIL" => await ListFolderAsync(client, FolderJunk, maxResults).ConfigureAwait(false),
            "FOLDER" => await ListFolderAsync(client, param1 ?? FolderInbox, maxResults).ConfigureAwait(false),
            "READ" => await ReadMessageAsync(client, param1).ConfigureAwait(false),
            "SEND" => await SendMailAsync(client, param1, param2, param3).ConfigureAwait(false),
            "SEARCH" => await SearchMailAsync(client, param1, maxResults).ConfigureAwait(false),
            "DELETE" => await DeleteMessageAsync(client, param1).ConfigureAwait(false),
            "MOVE" => await MoveMessageAsync(client, param1, param2).ConfigureAwait(false),
            "JUNK" => await MoveMessageAsync(client, param1, FolderJunk).ConfigureAwait(false),
            _ => "Unknown mode. Use: inbox, spam, folder, read, send, search, delete, move, junk",
        };
    }

    /// <summary>
    /// Creates a GraphServiceClient for testing purposes.
    /// </summary>
    /// <param name="client">The client to set, or null to reset.</param>
    internal static void SetTestClient(GraphServiceClient? client) => graphClient = client;

    private static GraphServiceClient? GetOrCreateClient()
    {
        if (graphClient is not null)
        {
            return graphClient;
        }

        var clientId = Environment.GetEnvironmentVariable(ClientIdEnvVar);
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        var tenantId = Environment.GetEnvironmentVariable(TenantIdEnvVar) ?? "consumers";

        // UnsafeAllowUnencryptedStorage is required on systems without a secure keychain
        var cacheOptions = new TokenCachePersistenceOptions
        {
            Name = "AgentDemo.Graph",
            UnsafeAllowUnencryptedStorage = true,
        };

        AuthenticationRecord? authRecord = null;

        // Try to load existing auth record for silent authentication
        if (File.Exists(AuthRecordPath))
        {
            try
            {
                using var stream = File.OpenRead(AuthRecordPath);
                authRecord = AuthenticationRecord.Deserialize(stream);
            }
            catch (IOException)
            {
                // Ignore errors loading auth record, will re-authenticate
            }
        }

        var credentialOptions = new DeviceCodeCredentialOptions
        {
            ClientId = clientId,
            TenantId = tenantId,
            TokenCachePersistenceOptions = cacheOptions,
            AuthenticationRecord = authRecord,
            DeviceCodeCallback = (code, _) =>
            {
                System.Console.WriteLine(code.Message);
                return Task.CompletedTask;
            },
        };

        var credential = new DeviceCodeCredential(credentialOptions);

        // First API call will trigger device code flow if needed
        // Token cache + auth record handle persistence automatically
        graphClient = new GraphServiceClient(credential, Scopes);

        // If no auth record exists, do a token request now to capture it
        // This saves the auth record after successful authentication
        if (authRecord is null)
        {
            try
            {
                var context = new TokenRequestContext(Scopes);
                _ = credential.GetToken(context, default);

                // Authentication succeeded, save the record for future silent auth
                authRecord = credential.AuthenticateAsync(context).GetAwaiter().GetResult();
                Directory.CreateDirectory(Path.GetDirectoryName(AuthRecordPath)!);
                using var stream = File.Create(AuthRecordPath);
                authRecord.Serialize(stream);
            }
            catch (AuthenticationFailedException)
            {
                // Auth failed or user cancelled - will prompt again next time
            }
        }

        return graphClient;
    }

    private static async Task<string> ListFolderAsync(GraphServiceClient client, string folderName, int maxResults)
    {
        try
        {
            var wellKnownFolder = folderName.ToUpperInvariant() switch
            {
                "INBOX" => FolderInbox,
                "JUNK" or "JUNKEMAIL" or "SPAM" => FolderJunk,
                "SENT" or "SENTITEMS" => FolderSent,
                "DRAFTS" => FolderDrafts,
                "DELETED" or "DELETEDITEMS" or "TRASH" => FolderDeleted,
                "ARCHIVE" => FolderArchive,
                _ => folderName,
            };

            var messages = await client.Me.MailFolders[wellKnownFolder].Messages.GetAsync(config =>
            {
                config.QueryParameters.Top = maxResults;
                config.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime", "isRead", "parentFolderId"];
                config.QueryParameters.Orderby = ["receivedDateTime desc"];
            }).ConfigureAwait(false);

            return messages?.Value is { Count: > 0 }
                ? FormatMessages(messages.Value, wellKnownFolder)
                : $"{folderName} folder is empty";
        }
        catch (ODataError ex)
        {
            return FormatError($"listing {folderName}", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            return FormatError(AuthError, ex);
        }
    }

    private static async Task<string> ReadMessageAsync(GraphServiceClient client, string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return "Error: Message ID required";
        }

        try
        {
            var message = await client.Me.Messages[messageId].GetAsync(config =>
                config.QueryParameters.Select = ["subject", "from", "toRecipients", "receivedDateTime", "body"]).ConfigureAwait(false);

            return message is null ? "Message not found" : FormatFullMessage(message);
        }
        catch (ODataError ex)
        {
            return FormatError("reading message", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            return FormatError(AuthError, ex);
        }
    }

    private static async Task<string> SendMailAsync(GraphServiceClient client, string? to, string? subject, string? body)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return "Error: Recipient email required";
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return "Error: Subject required";
        }

        try
        {
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Text, Content = body ?? string.Empty },
                ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = to } }],
            };

            await client.Me.SendMail.PostAsync(new SendMailPostRequestBody
            {
                Message = message,
                SaveToSentItems = true,
            }).ConfigureAwait(false);

            return $"Email sent to {to}";
        }
        catch (ODataError ex)
        {
            return FormatError("sending email", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            return FormatError(AuthError, ex);
        }
    }

    private static async Task<string> SearchMailAsync(GraphServiceClient client, string? query, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: Search query required";
        }

        try
        {
            var messages = await client.Me.Messages.GetAsync(config =>
            {
                config.QueryParameters.Top = maxResults;
                config.QueryParameters.Search = $"\"{query}\"";
                config.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime"];
            }).ConfigureAwait(false);

            return messages?.Value is { Count: > 0 }
                ? FormatMessages(messages.Value)
                : "No messages found";
        }
        catch (ODataError ex)
        {
            return FormatError("searching mail", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            return FormatError(AuthError, ex);
        }
    }

    private static async Task<string> DeleteMessageAsync(GraphServiceClient client, string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return "Error: Message ID required";
        }

        try
        {
            await client.Me.Messages[messageId].DeleteAsync().ConfigureAwait(false);
            return $"Deleted message {messageId[..8]}";
        }
        catch (ODataError ex)
        {
            return FormatError("deleting message", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            return FormatError(AuthError, ex);
        }
    }

    private static async Task<string> MoveMessageAsync(GraphServiceClient client, string? messageId, string? folder)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return "Error: Message ID required";
        }

        var destination = folder?.ToUpperInvariant() switch
        {
            "INBOX" => FolderInbox,
            "ARCHIVE" => FolderArchive,
            "DELETED" or "DELETEDITEMS" or "TRASH" => FolderDeleted,
            "JUNK" or "JUNKEMAIL" or "SPAM" => FolderJunk,
            "SENT" or "SENTITEMS" => FolderSent,
            "DRAFTS" => FolderDrafts,
            _ => folder ?? FolderDeleted,
        };

        try
        {
            await client.Me.Messages[messageId].Move.PostAsync(
                new Microsoft.Graph.Me.Messages.Item.Move.MovePostRequestBody
                {
                    DestinationId = destination,
                }).ConfigureAwait(false);
            return $"Moved message {messageId[..8]} to {destination}";
        }
        catch (ODataError ex)
        {
            return FormatError($"moving to {destination}", ex);
        }
        catch (AuthenticationFailedException ex)
        {
            return FormatError(AuthError, ex);
        }
    }

    private static string FormatMessages(IList<Message> messages, string? folderName = null)
    {
        var sb = new StringBuilder();
        if (folderName is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[{folderName}] ").AppendLine();
        }

        var index = 1;
        foreach (var msg in messages)
        {
            var read = msg.IsRead == true ? "✓" : "●";
            var from = msg.From?.EmailAddress?.Address ?? "Unknown";
            var date = msg.ReceivedDateTime?.ToString("g", CultureInfo.InvariantCulture) ?? "Unknown";
            sb.Append(CultureInfo.InvariantCulture, $"{index}. {read} {date} | {from}").AppendLine();
            sb.Append(CultureInfo.InvariantCulture, $"   {msg.Subject}").AppendLine();
            sb.Append(CultureInfo.InvariantCulture, $"   ID: {msg.Id}").AppendLine();
            index++;
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatFullMessage(Message msg)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Subject: {msg.Subject}").AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"From: {msg.From?.EmailAddress?.Address}").AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"Date: {msg.ReceivedDateTime?.ToString("g", CultureInfo.InvariantCulture)}").AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"To: {string.Join(", ", msg.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? [])}").AppendLine();
        sb.AppendLine();
        sb.AppendLine(msg.Body?.Content ?? "(No content)");
        return sb.ToString().TrimEnd();
    }

    private static string FormatError(string operation, ODataError ex) =>
        $"Error {operation}: {ex.Error?.Message ?? ex.Message}";

    private static string FormatError(string operation, AuthenticationFailedException ex) =>
        $"Error {operation}: {ex.Message}";
}
