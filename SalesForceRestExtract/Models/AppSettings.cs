namespace SalesForceRestExtract.Models
{
    /// <summary>
    ///     Used as the main App Settings Breaks the configuration into four:
    ///         1. Sales Force Configuration
    ///         2. File Locations
    ///         3. Connection Strings
    ///         4. Email Settings
    /// </summary>
    public class AppSettings
    {
        public string Title { get; set; }
        public SalesForce SalesForce { get; set; }
        public FileLocations FileLocations { get; set; }
        public ConnectionStrings ConnectionStrings { get; set; }
        public EmailSettings EmailSettings { get; set; }
        public SQLStoredProcedures SqlStoredProcedures { get; set; }
    }

    /// <summary>
    ///     Used for the Sales Force Configuration
    /// </summary>
    public class SalesForce
    {
        public string ClientSecret { get; set; }
        public string ClientId { get; set; }
        public string UserName { get; set; }
        public string UserSecurityToken { get; set; }
        public string UserPassword { get; set; }
        public string SalesForceLoginEndPoint { get; set; }
        public string QueryEndPoint { get; set; }
    }

    /// <summary>
    ///     Use for the stored procedures in case of changing of names
    /// </summary>
    public class SQLStoredProcedures
    {
        public string sqlEmail { get; set; }
        public string xmlParse { get; set; }
    }
    /// <summary>
    ///     Used for the File Locations Configuration
    /// </summary>
    public class FileLocations
    {
        public string QueryFile { get; set; }
        public string OutputDirectory { get; set; }
        public string LogDirectory { get; set; }
    }

    /// <summary>
    ///     Used for the connection strings
    /// </summary>
    public class ConnectionStrings
    {
        public string ConnectionProd { get; set; }
        public string ConnectionTest { get; set; }
    }

    /// <summary>
    ///     Used for the Email Settings
    /// </summary>
    public class EmailSettings
    {
        public string SMTPClient { get; set; }
        public int SMTPClientPort { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
    }

}