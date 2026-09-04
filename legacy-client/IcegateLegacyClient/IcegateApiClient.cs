using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using IcegateLegacyClient.Models;

namespace IcegateLegacyClient
{
    /// <summary>
    /// Thrown when the .NET 8 Icegate.Integration API returns a non-success response.
    /// ErrorMessage preserves the original ICEGATE business validation message where present.
    /// </summary>
    public class IcegateClientException : Exception
    {
        public int StatusCode { get; private set; }
        public string CorrelationId { get; private set; }
        public string ServerErrorMessage { get; private set; }

        public IcegateClientException(string message, int statusCode, string correlationId, string serverErrorMessage)
            : base(message)
        {
            StatusCode = statusCode;
            CorrelationId = correlationId;
            ServerErrorMessage = serverErrorMessage;
        }
    }

    /// <summary>
    /// .NET Framework 4.0 compatible client for the Icegate.Integration .NET 8 Web API.
    /// Uses HttpWebRequest/HttpWebResponse only (no HttpClient, no async/await - both are
    /// unavailable / discouraged on .NET Framework 4.0). This class talks ONLY to OUR API -
    /// it never contacts ICEGATE directly and never handles ICEGATE tokens.
    /// </summary>
    public class IcegateApiClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly int _timeoutMs;

        static IcegateApiClient()
        {
            // .NET Framework 4.0's SecurityProtocolType enum does not define Tls12 (added in 4.5),
            // so it is set via its underlying numeric value. Required because both our API and
            // ICEGATE UAT/PROD endpoints require TLS 1.2 or higher.
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // Tls12
            }
            catch (NotSupportedException)
            {
                // Underlying OS/.NET installation does not support TLS 1.2 - upload/ack calls
                // will fail with a connection error; this is surfaced to the caller as-is.
            }
        }

        public IcegateApiClient(string baseUrl, string apiKey, int timeoutMs)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentException("baseUrl is required", "baseUrl");
            }

            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : 120000;
        }

        /// <summary>
        /// Uploads an SCMTR JSON file to POST /api/icegate/inbound/upload.
        /// Returns the parsed response, including the ICEGATE uniqueId on success.
        /// </summary>
        public UploadApiResult UploadScmtrFile(string filePath, string localReferenceNo, string icegateId, string senderId)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("SCMTR file not found: " + filePath, filePath);
            }

            string boundary = "----IcegateLegacyBoundary" + Guid.NewGuid().ToString("N");
            byte[] body = BuildMultipartBody(boundary, filePath, localReferenceNo, icegateId, senderId);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_baseUrl + "/api/icegate/inbound/upload");
            request.Method = "POST";
            request.Timeout = _timeoutMs;
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.ContentLength = body.Length;
            ApplyCommonHeaders(request);

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(body, 0, body.Length);
            }

            string responseBody;
            int statusCode;
            GetResponseBody(request, out responseBody, out statusCode);

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            UploadApiResult result = serializer.Deserialize<UploadApiResult>(responseBody);

            if (result == null || !result.success)
            {
                string message = result != null ? result.errorMessage : "Empty response from server.";
                string correlationId = result != null ? result.correlationId : null;
                throw new IcegateClientException("SCMTR upload failed: " + message, statusCode, correlationId, message);
            }

            return result;
        }

        /// <summary>
        /// Retrieves the ICEGATE acknowledgement for a previously uploaded file via
        /// POST /api/icegate/ack. Callers should retry later (e.g. on a background timer)
        /// if this throws an IcegateClientException with StatusCode == 202 (ACK not yet available).
        /// </summary>
        public AckApiResult GetAcknowledgement(string senderId, string uniqueId)
        {
            if (string.IsNullOrEmpty(senderId))
            {
                throw new ArgumentException("senderId is required", "senderId");
            }
            if (string.IsNullOrEmpty(uniqueId))
            {
                throw new ArgumentException("uniqueId is required", "uniqueId");
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string jsonBody = serializer.Serialize(new
            {
                senderId = senderId,
                uniqueId = uniqueId
            });
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_baseUrl + "/api/icegate/ack");
            request.Method = "POST";
            request.Timeout = _timeoutMs;
            request.ContentType = "application/json";
            request.ContentLength = bodyBytes.Length;
            ApplyCommonHeaders(request);

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            string responseBody;
            int statusCode;
            GetResponseBody(request, out responseBody, out statusCode);

            AckApiResult result = serializer.Deserialize<AckApiResult>(responseBody);

            if (result == null || !result.success)
            {
                string message = result != null ? result.errorMessage : "Empty response from server.";
                string correlationId = result != null ? result.correlationId : null;
                throw new IcegateClientException("ACK retrieval failed: " + message, statusCode, correlationId, message);
            }

            return result;
        }

        private void ApplyCommonHeaders(HttpWebRequest request)
        {
            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Add("X-API-KEY", _apiKey);
            }
            request.Headers.Add("X-Correlation-Id", "LEGACY-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            request.Accept = "application/json";
        }

        /// <summary>
        /// Reads the response body whether the call succeeded or failed (HttpWebRequest throws
        /// a WebException for non-2xx responses - the body is still read from the exception's
        /// Response so error details from our API are not lost).
        /// </summary>
        private static void GetResponseBody(HttpWebRequest request, out string responseBody, out int statusCode)
        {
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    statusCode = (int)response.StatusCode;
                    responseBody = ReadStream(response.GetResponseStream());
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse)
                {
                    HttpWebResponse errorResponse = (HttpWebResponse)ex.Response;
                    statusCode = (int)errorResponse.StatusCode;
                    responseBody = ReadStream(errorResponse.GetResponseStream());
                    errorResponse.Close();
                }
                else
                {
                    // Network failure / timeout - no response body available.
                    throw new IcegateClientException(
                        "Network error calling Icegate.Integration API: " + ex.Message, 0, null, ex.Message);
                }
            }
        }

        private static string ReadStream(Stream stream)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static byte[] BuildMultipartBody(string boundary, string filePath, string localReferenceNo, string icegateId, string senderId)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                WriteTextField(stream, boundary, "localReferenceNo", localReferenceNo);
                WriteTextField(stream, boundary, "icegateId", icegateId);
                WriteTextField(stream, boundary, "senderId", senderId);

                string fileName = Path.GetFileName(filePath);
                string header = string.Format(
                    "--{0}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{1}\"\r\nContent-Type: application/json\r\n\r\n",
                    boundary, fileName);
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                stream.Write(headerBytes, 0, headerBytes.Length);

                byte[] fileBytes = File.ReadAllBytes(filePath);
                stream.Write(fileBytes, 0, fileBytes.Length);

                byte[] trailer = Encoding.UTF8.GetBytes("\r\n");
                stream.Write(trailer, 0, trailer.Length);

                byte[] footer = Encoding.UTF8.GetBytes("--" + boundary + "--\r\n");
                stream.Write(footer, 0, footer.Length);

                return stream.ToArray();
            }
        }

        private static void WriteTextField(Stream stream, string boundary, string name, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            string field = string.Format(
                "--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}\r\n",
                boundary, name, value);
            byte[] bytes = Encoding.UTF8.GetBytes(field);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
