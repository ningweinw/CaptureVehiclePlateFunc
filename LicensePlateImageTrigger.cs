using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using System.Data.SqlClient;

namespace CaptureVehiclePlateFunc
{
    public class LicensePlateImageTrigger
    {
        private readonly ILogger<LicensePlateImageTrigger> _logger;

        public LicensePlateImageTrigger(ILogger<LicensePlateImageTrigger> logger)
        {
            _logger = logger;
        }

        [Function(nameof(LicensePlateImageTrigger))]
        public async Task Run([BlobTrigger("vehicle-plate-image/{name}", Connection = "STORAGE_CONNECTION")] Stream stream, string name)
        {
            //using var blobStreamReader = new StreamReader(stream);
            //var content = await blobStreamReader.ReadToEndAsync();
            //_logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}");
            _logger.LogInformation($"LicensePlateImageTrigger processing blob, name: {name}");

            try
            {
                // Cognitive Service key and endpoint
                var subscriptionKey = Environment.GetEnvironmentVariable("COMPUTER_VISION_SUBSCRIPTION_KEY");
                var endpoint = Environment.GetEnvironmentVariable("COMPUTER_VISION_ENDPOINT");
                _logger.LogInformation($"Computer Vision endpoint: {endpoint}");

                // Read the license plate from the image
                var plateText = readFromImage(endpoint, subscriptionKey, stream);
                _logger.LogInformation($"License plate text:\n{plateText}");

                // Write result into SQL DB
                var sqlConnStr = Environment.GetEnvironmentVariable("SQLDB_CONNECTION");
                _logger.LogInformation("Connect to DB...");
                using (SqlConnection conn = new SqlConnection(sqlConnStr))
                {
                    conn.Open();
                    var sql = "INSERT INTO VehiclePlate VALUES (1, GETDATE(), '" + 
                        plateText + "', '" + name + "')";

                    _logger.LogInformation($"Execute statement: {sql} ...");
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Execute the command and log the # rows affected.
                        var rows = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation($"{rows} rows were inserted");
                    }
                    conn.Close();
                }
            }
            catch(System.Exception ex)
            {
                _logger.LogTrace($"Error: {ex.Message}");
            }
        }

        public string readFromImage(string endpoint, string key, Stream stream)
        {
            _logger.LogInformation($"Calling Azure AI Vision to read from the image...");

            var client = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
            ImageAnalysisResult result = client.Analyze(BinaryData.FromStream(stream), 
                VisualFeatures.Read);

            // extract the text from the result
            var text = "";
            if(result.Read != null)
            {
                foreach(var line in result.Read.Blocks.SelectMany(block => block.Lines))
                {
                    if(text.Length > 0) text += "\n";
                    text += line.Text;
                }
            }
            _logger.LogInformation($"AI Vision Read completed. Text: {text}");
            return text;
        }
    }
}
