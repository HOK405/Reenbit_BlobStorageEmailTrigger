using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

public class EmailFunction
{
    private readonly ILogger<EmailFunction> _logger;
    private readonly IConfiguration _configuration;

    private readonly string _blobServiceConnectionString;
    private readonly string _containerName;
    private readonly string _emailSenderName;
    private readonly string _emailSenderPass;
    private readonly int _emailSenderPort;
    private readonly string _emailSenderHost;

    public EmailFunction(ILogger<EmailFunction> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _blobServiceConnectionString = _configuration["CONNECTION_STRING"];
        _containerName = _configuration["CONTAINER_NAME"];
        _emailSenderName = _configuration["EMAIL_SENDER_NAME"];
        _emailSenderPass = _configuration["EMAIL_SENDER_PASS"];
        _emailSenderPort = int.Parse(_configuration["EMAIL_SENDER_PORT"]);
        _emailSenderHost = _configuration["EMAIL_SENDER_HOST"];

        ValidateConfiguration();
    }

    [Function(nameof(EmailFunction))]
    public async Task Run([BlobTrigger("hok405blobcontainer/{name}")] Stream stream, string name, IDictionary<string, string> metadata, string blobTrigger)
    {
        _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name}");

        if (metadata.TryGetValue("email", out var userEmail))
        {
            _logger.LogInformation($"Checking email validity for: {userEmail}");

            if (IsValidEmail(userEmail))
            {
                _logger.LogInformation($"Sending email to: {userEmail}");

                try
                {
                    var blobUrlWithSas = GenerateBlobSasUrl(_containerName, name);
                    await SendEmailAsync(userEmail, name, blobUrlWithSas);
                    _logger.LogInformation($"Email sent successfully to {userEmail}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send email. Exception: {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning($"Invalid email address format: {userEmail}");
            }
        }
        else
        {
            _logger.LogWarning("Email metadata not found.");
        }

    }

    private string GenerateBlobSasUrl(string containerName, string blobName)
    {
        var blobServiceClient = new BlobServiceClient(_blobServiceConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), 
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            Protocol = SasProtocol.Https,
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        sasBuilder.Version = "2022-11-02"; 

        var sasToken = blobClient.GenerateSasUri(sasBuilder).Query.TrimStart('?');

        return $"{blobClient.Uri}?{sasToken}";
    }

    private async Task SendEmailAsync(string toEmail, string fileName, string blobUrlWithSas)
    {
        var smtpClient = new SmtpClient(_emailSenderHost)
        {
            Port = _emailSenderPort,
            Credentials = new NetworkCredential(_emailSenderName, _emailSenderPass),
            EnableSsl = true,
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_emailSenderName),
            Subject = "Your file has been uploaded",
            Body = $"Hello, your file {fileName} has been uploaded successfully. You can download it using the following secure link, valid for 1 hour: <a href=\"{blobUrlWithSas}\">{blobUrlWithSas}</a>",
            IsBodyHtml = true,
        };
        mailMessage.To.Add(toEmail);

        await smtpClient.SendMailAsync(mailMessage);
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var mailAddress = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_blobServiceConnectionString))
            throw new ArgumentNullException(nameof(_blobServiceConnectionString), "Blob service connection string is not configured.");

        if (string.IsNullOrWhiteSpace(_containerName))
            throw new ArgumentNullException(nameof(_containerName), "Container name is not configured.");

        if (string.IsNullOrWhiteSpace(_emailSenderName))
            throw new ArgumentNullException(nameof(_emailSenderName), "Email sender name is not configured.");

        if (string.IsNullOrWhiteSpace(_emailSenderPass))
            throw new ArgumentNullException(nameof(_emailSenderPass), "Email sender password is not configured.");

        if (string.IsNullOrWhiteSpace(_emailSenderHost))
            throw new ArgumentNullException(nameof(_emailSenderHost), "Email sender host is not configured.");

        if (_emailSenderPort <= 0)
            throw new ArgumentOutOfRangeException(nameof(_emailSenderPort), "Email sender port is not configured correctly.");
    }
}
