using System;
using System.Configuration;
using System.Threading;
using IcegateLegacyClient.Models;

namespace IcegateLegacyClient
{
    /// <summary>
    /// Console demo of the legacy application flow described in the integration spec:
    /// select SCMTR JSON -> upload to our .NET 8 API -> receive uniqueId -> save uniqueId ->
    /// wait/retry ACK -> POST /api/icegate/ack -> receive ACK -> save/display ACK -> update
    /// local status. In the real ASP.NET / .NET Framework 4.0 application this logic would
    /// live in a button click handler / business layer instead of Main().
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            string baseUrl = ConfigurationManager.AppSettings["OurApiBaseUrl"];
            string apiKey = ConfigurationManager.AppSettings["OurApiKey"];
            string icegateId = ConfigurationManager.AppSettings["IcegateId"];
            string senderId = ConfigurationManager.AppSettings["SenderId"];
            int timeoutMs = ParseIntOrDefault(ConfigurationManager.AppSettings["RequestTimeoutMs"], 120000);

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: IcegateLegacyClient.exe <path-to-scmtr.json> [localReferenceNo]");
                return;
            }

            string filePath = args[0];
            string localReferenceNo = args.Length > 1 ? args[1] : "LOCALREF-" + DateTime.Now.ToString("yyyyMMddHHmmss");

            IcegateApiClient client = new IcegateApiClient(baseUrl, apiKey, timeoutMs);

            // Step 1: Upload the SCMTR JSON file selected by the user.
            Console.WriteLine("Uploading SCMTR file: " + filePath);
            string uniqueId;
            try
            {
                UploadApiResult uploadResult = client.UploadScmtrFile(filePath, localReferenceNo, icegateId, senderId);
                uniqueId = uploadResult.data.uniqueId;

                Console.WriteLine("Upload succeeded.");
                Console.WriteLine("  validationStatus : " + uploadResult.data.validationStatus);
                Console.WriteLine("  uniqueId         : " + uniqueId);
                Console.WriteLine("  correlationId    : " + uploadResult.correlationId);

                // Step: Save uniqueId against the local transaction record.
                SaveUniqueIdToLocalDatabase(localReferenceNo, uniqueId);
            }
            catch (IcegateClientException ex)
            {
                // Business validation failures (invalid sender, malformed JSON, etc.) must NOT
                // be retried automatically - surface them to the user/operator.
                Console.WriteLine("Upload FAILED: " + ex.Message);
                Console.WriteLine("  statusCode    : " + ex.StatusCode);
                Console.WriteLine("  correlationId : " + ex.CorrelationId);
                return;
            }

            // Step 2: Poll for the ACK with a bounded retry/backoff loop (the ACK is generated
            // asynchronously by ICEGATE and is not necessarily available immediately).
            const int maxAttempts = 5;
            const int delayBetweenAttemptsMs = 15000;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                Console.WriteLine(string.Format("Requesting ACK (attempt {0} of {1})...", attempt, maxAttempts));

                try
                {
                    AckApiResult ackResult = client.GetAcknowledgement(senderId, uniqueId);

                    Console.WriteLine("ACK received.");
                    Console.WriteLine("  responseDesc : " + ackResult.data.responseDesc);
                    Console.WriteLine("  fileName     : " + ackResult.data.fileName);

                    UpdateLocalStatus(localReferenceNo, "ACK_RECEIVED");
                    return;
                }
                catch (IcegateClientException ex)
                {
                    if (ex.StatusCode == 202)
                    {
                        Console.WriteLine("ACK not yet available: " + ex.Message + " - will retry.");
                        Thread.Sleep(delayBetweenAttemptsMs);
                        continue;
                    }

                    Console.WriteLine("ACK retrieval FAILED: " + ex.Message);
                    UpdateLocalStatus(localReferenceNo, "ACK_FAILED");
                    return;
                }
            }

            Console.WriteLine("ACK still not available after " + maxAttempts + " attempts. Try again later.");
            UpdateLocalStatus(localReferenceNo, "ACK_PENDING");
        }

        private static void SaveUniqueIdToLocalDatabase(string localReferenceNo, string uniqueId)
        {
            // Placeholder for the existing application's own persistence logic
            // (ADO.NET / Entity Framework / stored procedure - whatever it already uses).
            Console.WriteLine(string.Format("[local-db] LocalReferenceNo={0} -> UniqueId={1}", localReferenceNo, uniqueId));
        }

        private static void UpdateLocalStatus(string localReferenceNo, string status)
        {
            Console.WriteLine(string.Format("[local-db] LocalReferenceNo={0} -> Status={1}", localReferenceNo, status));
        }

        private static int ParseIntOrDefault(string value, int defaultValue)
        {
            int parsed;
            if (int.TryParse(value, out parsed))
            {
                return parsed;
            }
            return defaultValue;
        }
    }
}
