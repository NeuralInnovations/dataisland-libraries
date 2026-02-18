namespace Dataisland.MQ
{
    [Serializable]
    public class RabbitMqOptions
    {
        public string? ConnectionString { get; set; }
        public string Host { get; set; } = null!;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;

        public void ApplyConnectionString()
        {
            if (string.IsNullOrEmpty(ConnectionString)) return;

            var uri = new Uri(ConnectionString);
            Host = uri.Port > 0 && uri.Port != 5672
                ? $"{uri.Host}:{uri.Port}"
                : uri.Host;
            VirtualHost = uri.AbsolutePath == "/" || string.IsNullOrEmpty(uri.AbsolutePath)
                ? "/"
                : Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':');
                Username = Uri.UnescapeDataString(parts[0]);
                Password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            }
        }
    }
}
