using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Net.Mail;

namespace EmailTrigger.Tests
{
    public class EmailFunctionTests
    {
        private readonly Mock<ILogger<EmailFunction>> _mockLogger = new Mock<ILogger<EmailFunction>>();
        private readonly Mock<IConfiguration> _mockConfiguration = new Mock<IConfiguration>();
        private EmailFunction _emailFunction;

        public EmailFunctionTests()
        {
            _mockConfiguration.Setup(c => c["CONNECTION_STRING"]).Returns("DefaultEndpointsProtocol=https;AccountName=default;AccountKey=default;EndpointSuffix=core.windows.net");
            _mockConfiguration.Setup(c => c["CONTAINER_NAME"]).Returns("container-name");
            _mockConfiguration.Setup(c => c["EMAIL_SENDER_NAME"]).Returns("email-sender-name");
            _mockConfiguration.Setup(c => c["EMAIL_SENDER_PASS"]).Returns("email-sender-pass");
            _mockConfiguration.Setup(c => c["EMAIL_SENDER_PORT"]).Returns("587");
            _mockConfiguration.Setup(c => c["EMAIL_SENDER_HOST"]).Returns("smtp.email.com");

            _emailFunction = new EmailFunction(_mockLogger.Object, _mockConfiguration.Object);
        }

        [Fact]
        public void IsValidEmail_InvalidEmail_ReturnsFalse()
        {
            // Arrange
            var emailFunction = new EmailFunction(_mockLogger.Object, _mockConfiguration.Object);
            var methodInfo = typeof(EmailFunction).GetMethod("IsValidEmail", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (bool)methodInfo.Invoke(emailFunction, new object[] { "invalid-email" });

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_ValidEmail_ReturnsTrue()
        {
            // Arrange
            var emailFunction = new EmailFunction(_mockLogger.Object, _mockConfiguration.Object);
            var methodInfo = typeof(EmailFunction).GetMethod("IsValidEmail", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (bool)methodInfo.Invoke(emailFunction, new object[] { "email@example.com" });

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Constructor_AllConfigurationsValid_DoesNotThrowException()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string>
            {
                {"CONNECTION_STRING", "ValidConnectionString"},
                {"CONTAINER_NAME", "ValidContainerName"},
                {"EMAIL_SENDER_NAME", "ValidEmailSenderName"},
                {"EMAIL_SENDER_PASS", "ValidEmailSenderPass"},
                {"EMAIL_SENDER_PORT", "587"},
                {"EMAIL_SENDER_HOST", "ValidEmailSenderHost"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            // Act & Assert
            var exception = Record.Exception(() => new EmailFunction(new NullLogger<EmailFunction>(), configuration));
            Assert.Null(exception);
        }

        [Theory]
        [InlineData("CONNECTION_STRING", typeof(ArgumentNullException))]
        [InlineData("CONTAINER_NAME", typeof(ArgumentNullException))]
        [InlineData("EMAIL_SENDER_NAME", typeof(ArgumentNullException))]
        [InlineData("EMAIL_SENDER_PASS", typeof(ArgumentNullException))]
        [InlineData("EMAIL_SENDER_PORT", typeof(ArgumentOutOfRangeException))]
        [InlineData("EMAIL_SENDER_HOST", typeof(ArgumentNullException))]
        public void Constructor_MissingOrInvalidConfiguration_ThrowsException(string missingKey, Type expectedExceptionType)
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string>
            {
                {"CONNECTION_STRING", "ValidConnectionString"},
                {"CONTAINER_NAME", "ValidContainerName"},
                {"EMAIL_SENDER_NAME", "ValidEmailSenderName"},
                {"EMAIL_SENDER_PASS", "ValidEmailSenderPass"},
                {"EMAIL_SENDER_PORT", "587"},
                {"EMAIL_SENDER_HOST", "ValidEmailSenderHost"}
            };

            if (missingKey == "EMAIL_SENDER_PORT")
            {
                inMemorySettings[missingKey] = "0"; 
            }
            else
            {
                inMemorySettings.Remove(missingKey);
            }

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            // Act & Assert
            var exception = Record.Exception(() => new EmailFunction(new NullLogger<EmailFunction>(), configuration));

            Assert.NotNull(exception);
            Assert.IsType(expectedExceptionType, exception);
        }

        [Fact]
        public async Task Run_InvalidEmail_NotSending()
        {
            // Arrange          
            var stream = new MemoryStream();
            var name = "testBlob.docx";
            var metadata = new Dictionary<string, string> { { "email", "test@example.com" } };
            var blobTrigger = "hok405blobcontainer/testBlob.docx";      

            // Act
            await _emailFunction.Run(stream, name, metadata, blobTrigger);

            // Assert
             _mockLogger.Verify(
                 x => x.Log(
                     LogLevel.Error,
                     It.IsAny<EventId>(),
                     It.Is<It.IsAnyType>((object v, Type _) => v.ToString().Contains("Failed to send email. Exception: No valid combination of account information found.")),
                     It.IsAny<Exception>(),
                     It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                 Times.Once);
        }
    }
}