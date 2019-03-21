using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SalesForceRestExtract.Models;

namespace SalesForceRestExtract.Controller
{
    public class SalesForceController
    {
        /// <summary>
        ///     The Sales Force for client ID
        /// </summary>
        private static string _clientId;

        /// <summary>
        ///     The Sales Force client secret
        /// </summary>
        private static string _clientSecret;

        /// <summary>
        ///     The Sales Force Username being used
        /// </summary>
        private static string _username;

        /// <summary>
        ///     The Sales force user security token
        /// </summary>
        private static string _securityToken;

        /// <summary>
        ///     The Sales force user password
        /// </summary>
        private static string _password;

        /// <summary>
        ///     Sales Force login endpoint
        /// </summary>
        private static string _salesForceLoginEndpoint;

        /// <summary>
        ///     Sales Force Query Endpoint, api version
        /// </summary>
        private static string _salesForceQueryEndpoint;

        /// <summary>
        ///     Query file location
        /// </summary>
        private static string _queryFileLocation;

        /// <summary>
        ///     Output directory for the results files
        /// </summary>
        private static string _outputDirectory;

        /// <summary>
        ///     End connection string to SQL
        /// </summary>
        private static string _connectionString;

        /// <summary>
        ///     SMTP Address
        /// </summary>
        private static string _smptClient;

        /// <summary>
        ///     SMTP Port Number
        /// </summary>
        private static int _smtpClientPort;

        /// <summary>
        ///     From Address for the emails of errors
        /// </summary>
        private static string _fromAddress;

        /// <summary>
        ///     To Address for the emails of errors
        /// </summary>
        private static string _toAddress;

        /// <summary>
        ///     Used as the name of the email stored procedure name
        /// </summary>
        private static string _sqlEmailSp;

        /// <summary>
        ///     Used as the xml parse stored procedure name
        /// </summary>
        private static string _xmlParseSp;

        /// <summary>
        ///     Connected to sales force message
        /// </summary>
        private static readonly string _connectedToSalesforce = "Connected to SalesForce";

        /// <summary>
        ///     Data Extract started log message
        /// </summary>
        private static readonly string _dataExtractStarted = "Data Extract Started";

        /// <summary>
        ///     Used for the oauth token
        /// </summary>
        private static string _oauthToken;

        /// <summary>
        ///     Used for the service URL
        /// </summary>
        private static string _serviceUrl;

        /// <summary>
        ///     Used for the logger instance of ILogger
        /// </summary>
        private static ILogger _logger;

        /// <summary>
        ///     Used for the AppSettings configuration
        /// </summary>
        private readonly AppSettings _config;

        /// <summary>
        ///     Used for the logging directory
        /// </summary>
        private static string _logDirectory;

        /// <summary>
        ///     Constructor for the Sales Force Controller
        /// </summary>
        /// <param name="config">
        ///     Requires an <see cref="IOptions{TOptions}" /> for the configuration
        /// </param>
        /// <param name="logger">
        ///     Requires an <see cref="ILogger{TCategoryName}" /> for the logger
        /// </param>
        public SalesForceController(IOptions<AppSettings> config,
            ILogger<SalesForceController> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        ///     Used to start the Sales Force Extraction
        /// </summary>
        public void Start()
        {
            _logger.LogInformation(_dataExtractStarted);
            LoadConfiguration();
            CompressXMLFiles(_outputDirectory);
            ConnectToSalesForce().Wait();
            ProcessQueryFile().Wait();
        }


        /// <summary>
        ///     Connect to sales force with the id, secret, username and password
        /// </summary>
        /// <returns>
        ///     Returns a basic task
        /// </returns>
        private static async Task ConnectToSalesForce()
        {
            var authClient = new HttpClient();
            HttpContent content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"grant_type", "password"},
                    {"client_id", _clientId},
                    {"client_secret", _clientSecret},
                    {"username", _username},
                    {"password", _password}
                }
            );
            try
            {
                var message = await authClient.PostAsync(_salesForceLoginEndpoint, content);
                if (message.StatusCode == HttpStatusCode.OK)
                {
                    var responseString = await message.Content.ReadAsStringAsync();
                    var obj = JObject.Parse(responseString);
                    if (string.IsNullOrEmpty((string) obj["error"]))
                    {
                        _oauthToken = (string) obj["access_token"];
                        _serviceUrl = (string) obj["instance_url"];

                        _logger.LogDebug(_connectedToSalesforce);
                    }
                }

                else
                {
                    throw new ArgumentException("Error was found while connecting to database:" + Environment.NewLine +
                                                "<br> Status Code: " +
                                                message.StatusCode + Environment.NewLine +
                                                "<br> Response from Server: " +
                                                message
                    );
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error found while connecting to sales force: " + e.Message);
                SendEmail("Error found while connecting to sales force with Sales Force Extract.",
                    "The following error(s) occurred while connecting to sales force: " + e.Message);
                Environment.Exit(0);
            }
        }

        /// <summary>
        ///     Used to load configuration settings
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                var salesForceConfig = _config.SalesForce;
                var fileLocations = _config.FileLocations;
                var connectionStrings = _config.ConnectionStrings;
                var emailSettings = _config.EmailSettings;
                var storedProcedures = _config.SqlStoredProcedures;

                _clientSecret =
                    Convert.ToString(salesForceConfig.ClientSecret);
                _clientId =
                    Convert.ToString(salesForceConfig.ClientId);
                _username =
                    Convert.ToString(salesForceConfig.UserName);
                _securityToken =
                    Convert.ToString(salesForceConfig.UserSecurityToken);
                _password = DecryptPassword(Convert.ToString(salesForceConfig.UserPassword)) + _securityToken;
                _salesForceLoginEndpoint =
                    Convert.ToString(salesForceConfig.SalesForceLoginEndPoint);
                _salesForceQueryEndpoint =
                    Convert.ToString(salesForceConfig.QueryEndPoint);
                _queryFileLocation =
                    Convert.ToString(fileLocations.QueryFile);
                _outputDirectory =
                    Convert.ToString(fileLocations.OutputDirectory);
                _logDirectory =
                    Convert.ToString(fileLocations.LogDirectory);
#if DEBUG
                _connectionString =
                    Convert.ToString(connectionStrings.ConnectionTest);
#else
                 _connectionString =
                    Convert.ToString(connectionStrings.ConnectionProd);
#endif
                _smtpClientPort = emailSettings.SMTPClientPort;
                _smptClient = emailSettings.SMTPClient;
                _fromAddress = emailSettings.FromAddress;
                _toAddress = emailSettings.ToAddress;
                _sqlEmailSp = storedProcedures.sqlEmail;
                _xmlParseSp = storedProcedures.xmlParse;
            }
            catch (Exception e)
            {
                _logger.LogError("Loading Configuration Failed: " + e.Message);
                SendEmail("Error Loading configuration",
                    "The following error(s) occurred while loading configuration file: " + e.Message);
                Environment.Exit(0);
            }

            _logger.LogDebug("Configuration Loaded Successfully");
        }


        /// <summary>
        ///     Performs a query on Salesforce based on the query end point
        /// </summary>
        /// <param name="soqlQuery">
        ///     Requires a <see cref="string" /> of the SOQL Query
        /// </param>
        /// <returns>
        ///     Returns a Task of a string
        /// </returns>
        private static async Task<string> PerformQuery(string soqlQuery)
        {
            string queryAddress;

            if (!Regex.IsMatch(soqlQuery, @"\b(select|SELECT)\b"))
                queryAddress = _serviceUrl + soqlQuery;
            else queryAddress = _serviceUrl + _salesForceQueryEndpoint + soqlQuery;

            var queryClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, queryAddress);
            request.Headers.Add("Authorization", "Bearer " + _oauthToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                _logger.LogDebug("Running Query: " + soqlQuery);
                var response = await queryClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();
                try
                {
                    //Used to check if it has multiple results, don't like this method but seems to cause less issues and get a better result for tickets
                    JObject.Parse(result);
                }
                catch (Exception)
                {
                    try
                    {
                        // Grabs the first item in the json array, there has only ever been one at a time, don't know why it comes as multiple results
                        result = JArray.Parse(result).First.ToString();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("JSON Parse Failed: " + e.Message);
                        SendEmail("JSON Parse Failed",
                            "The following error(s) occurred: " + e.Message + "<br>" + Environment.NewLine +
                            "The following result errored: " + result);
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                _logger.LogError("Query Failed: " + e.Message);
                _logger.LogError("Query: " + soqlQuery);
                SendEmail("Quering Sales Force Failed",
                    "The following error occurred while quering Sales Force: " + e.Message + "<br>" +
                    Environment.NewLine +
                    "The following query failed: " + soqlQuery);
                Environment.Exit(0);
            }

            return "";
        }

        /// <summary>
        ///     Processes the Query File and submits the query to Salesforce
        /// </summary>
        /// <returns>
        ///     Returns a Task
        /// </returns>
        private static async Task ProcessQueryFile()
        {
            try
            {
                string line;
                var sr = new StreamReader(_queryFileLocation);
                while ((line = sr.ReadLine()) != null)
                {
                    if (line[0] != '*')
                    {
                        // First item is file name/Table, second item is query
                        var lineSplit = line.Split(":");
                        var resultsFromQuery = await PerformQuery(lineSplit[1]);
                        var initialQuery = new XmlDocument();

                        try
                        {
                            _logger.LogDebug("Results from Query: " + resultsFromQuery);
                            //TODO - Somehow make it so that when it is in this spot, it will check
                            var jobject = JObject.Parse(resultsFromQuery);
                            if (jobject.ContainsKey("errorCode"))
                            {
                                _logger.LogError("Error in Query: " + jobject["errorCode"]);
                                SendEmail("Error when running Sales Force Query",
                                    "Error when running Sales Force Query: <br>" + jobject["errorCode"] +
                                    "<br>\nResults From Query: " + jobject["message"]);
                                continue;
                            }
                            initialQuery = JsonConvert.DeserializeXmlNode(resultsFromQuery, "recordsFound");
                            XmlNodeList attributesNodes;
                            try
                            {
                                attributesNodes = initialQuery.SelectNodes("//attributes");
                            }
                            catch (XPathException e)
                            {
                                _logger.LogError("Unable to select attributes " + e.Message);
                                throw;
                            }

                            foreach (XmlNode xmlNode in attributesNodes) xmlNode.ParentNode.RemoveChild(xmlNode);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError("Unable to Convert to XML, check query: " + e.Message);
                            SendEmail("Converting json respond to XML Failed",
                                "Converting the json respond to XML failed: <br>" + e.Message +
                                "<br>\nRunning Query: " +
                                lineSplit[0] + "<br>\nResults From Query: " + resultsFromQuery[0]);
                            Environment.Exit(0);
                        }

                        try
                        {
                            using (var outputFile = new StreamWriter(Path.Combine(_outputDirectory,
                                lineSplit[0] + "-" + $"{DateTime.Now:yyyy-MM-dd_hh-mm-ss-fff}" +
                                ".xml")))
                            {
                                outputFile.WriteLine(initialQuery.InnerXml);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError("Unable to write file.  The following error(s) occurred: " + e.Message);
                            SendEmail("Failed to write query results file.",
                                "Writing query results file failed due to the following error(s): " + e.Message +
                                "\nFile Location:" + lineSplit[0] + "-" + $"{DateTime.Now:yyyy-MM-dd_hh-mm-ss-fff}" +
                                ".xml");
                            Environment.Exit(0);
                        }

                        SendToStoredProcedure(lineSplit[0], initialQuery.InnerXml);
                    }

                    _logger.LogInformation("Data Extract Completed");
                }
            }
            catch (Exception e)
            {
                _logger.LogError("File cannot be read: " + e.Message);
                SendEmail("Sales1Reading Query File Failed",
                    "Reading query file failed with the following error: " + e.Message + "\n Location: " +
                    _queryFileLocation);
                Environment.Exit(0);
            }
        }

        /// <summary>
        ///     Function to send the xml to a stored procedure
        /// </summary>
        /// <param name="type">
        ///     Requires a <see cref="string" /> for the type which is the table
        /// </param>
        /// <param name="xmltoSend">
        ///     Requires a <see cref="string" /> of the XML to pass to the stored procedure
        /// </param>
        private static void SendToStoredProcedure(string type, string xmltoSend)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    var command =
                        new SqlCommand(_xmlParseSp, connection) {CommandType = CommandType.StoredProcedure};
                    command.Parameters.Add(new SqlParameter("@Type", type));
                    command.Parameters.Add(new SqlParameter("@xml_text", xmltoSend));
                    command.CommandTimeout = 10;
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    _logger.LogError("Command returned error from SQL: " + e.Message);
                    SendEmail("Unable to connect to SQL stored Procedure",
                        "Unable to connect to SQL Stored Procedure.\n" +
                        "The following error(s) occurred while running stored procedure: " + e.Message + "\n" +
                        "The following information was sent to the SQL Server: \n\t Type: " + type + "\n\t XMLSent: " +
                        xmltoSend);
                    Environment.Exit(0);
                }
            }
        }

        /// <summary>
        ///     Decrypts the password using the cipher file
        /// </summary>
        /// <param name="passwordToDecrypt">
        ///     Takes a <see cref="string" /> for the password to decrypt via the Sales Force Security token.
        /// </param>
        /// <returns></returns>
        private static string DecryptPassword(string passwordToDecrypt)
        {
            var cipher = new Cipher();

            var decryptedPass = cipher.Decrypt(passwordToDecrypt, _securityToken);
            return decryptedPass;
        }

        /// <summary>
        ///     Work around that was needed to use SQL Server to send emails
        /// </summary>
        /// <param name="subject">Requires a <see cref="string" /> for the subject</param>
        /// <param name="body">Requires a <see cref="string" /> for the body of the email.</param>
        /// <returns></returns>
        private static void SendEmail(string subject, string body)
        {

            using (var connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    var command =
                        new SqlCommand(_sqlEmailSp, connection) { CommandType = CommandType.StoredProcedure };
                    command.Parameters.Add(new SqlParameter("@recepients", _toAddress));
                    command.Parameters.Add(new SqlParameter("@subject", subject));
                    command.Parameters.Add(new SqlParameter("@body", body));
                    command.CommandTimeout = 10;
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    _logger.LogError("Command returned error from SQL: " + e.Message);
                    SendEmail("Unable to connect to SQL stored Procedure",
                        "Unable to connect to SQL Stored Procedure.\n" +
                        "The following error(s) occurred while running stored procedure: " + e.Message + "\n" +
                        "The following information was sent to the SQL Server: \n\t Type: " + subject + "\n\t XMLSent: " +
                        body);
                    Environment.Exit(0);
                }
            }

        }

        /// <summary>
        ///     Used to compress all the xml files in the results directory
        /// </summary>
        /// <param name="directoryToCompress">Requires a <see cref="string" /> for the directory to compress</param>
        private static void CompressXMLFiles(string directoryToCompress)
        {
            //get a list of files
            var filesToZip = Directory.GetFiles(directoryToCompress, "*.xml", SearchOption.AllDirectories);
            var zipFileName = $"SalesForceExtract-{DateTime.Now:yyyy-MM-dd}.zip";
            if (File.Exists(directoryToCompress + "\\" + zipFileName))
            {
                using (var zipToOpen = new FileStream(directoryToCompress + "\\" + zipFileName, FileMode.Open))
                {
                    using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        foreach (var fileToZip in filesToZip)
                        {
                            if (new FileInfo(fileToZip).Extension == ".zip") continue;

                            //exclude some file names maybe?
                            if (fileToZip.Contains("node_modules")) continue;


                            archive.CreateEntryFromFile(fileToZip, Path.GetFileName(fileToZip));
                            File.Delete(fileToZip);
                        }
                    }
                }
            }
            else
            {
                DailyArchive(directoryToCompress, directoryToCompress + "\\Archive");
                using (var zipMs = new MemoryStream())
                {
                    using (var zipArchive = new ZipArchive(zipMs, ZipArchiveMode.Create, true))
                    {
                        //loop through files to add
                        foreach (var fileToZip in filesToZip)
                        {
                            //exclude some files? -I don't want to ZIP other .zips in the folder.
                            if (new FileInfo(fileToZip).Extension == ".zip") continue;

                            //exclude some file names maybe?
                            if (fileToZip.Contains("node_modules")) continue;

                            //read the file bytes

                            zipArchive.CreateEntryFromFile(fileToZip, Path.GetFileName(fileToZip));
                        }
                    }

                    using (var finalZipFileStream =
                        new FileStream(directoryToCompress + "\\" + zipFileName, FileMode.Create))
                    {
                        zipMs.Seek(0, SeekOrigin.Begin);
                        zipMs.CopyTo(finalZipFileStream);
                    }
                }
            }
        }



        /// <summary>
        ///     Used to do a daily archive of two directories
        /// </summary>
        /// <param name="directoryOfResults"></param>
        /// <param name="directoryofArchive"></param>
        private static void DailyArchive(string directoryOfResults, string directoryofArchive)
        {
            var zipFileNameYesterday = $"SalesForceExtract-{DateTime.Now.AddDays(-1):yyyy-MM-dd}.zip";
            var yesterdayFileFullName = directoryOfResults + "\\" + zipFileNameYesterday;
            if (File.Exists(yesterdayFileFullName))
            {
                if (!Directory.Exists(directoryofArchive)) Directory.CreateDirectory(directoryofArchive);
                File.Move(yesterdayFileFullName, directoryofArchive + "\\" + zipFileNameYesterday);
            }
            else
            {
                if (!File.Exists(zipFileNameYesterday)) return;
                File.Move(yesterdayFileFullName, directoryofArchive + "\\" + zipFileNameYesterday);
            }
        }
    }
}