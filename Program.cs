namespace TwitterDiscordWebhook
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using System.Text;
    using System.Text.Json;

    public static class Program
    {
        public const string WEBHOOK_USERNAME = "New Tweet";
        public const string WEBHOOK_AVATAR_URL = "https://nitter.poast.org/apple-touch-icon.png";
        public const string CONTENT_FORMAT_STRING = "# New Tweet by ``{0}`` at <t:{1}:F>\n```\n{2}\n```\nLink: <{3}>";

        public static async Task Main(string[] args)
        {
            DotEnv.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0");
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(await client.GetStringAsync($"{Environment.GetEnvironmentVariable("NITTER_INSTANCE")}/{Environment.GetEnvironmentVariable("TWITTER_USER")}/rss"));
                string updated = doc.GetElementsByTagName("pubDate")[0]!.InnerText;
                DateTime updatedDate = DateTime.Parse(updated);
                long timestamp = ((DateTimeOffset)updatedDate).ToUnixTimeSeconds();
                bool fileNeedsUpdate = true;
                if (File.Exists("lastUpdatedValue"))
                {
                    string lastUpdatedValue = File.ReadAllText("lastUpdatedValue");
                    if (lastUpdatedValue.Trim().Equals(updated.Trim()))
                    {
                        Console.WriteLine("Already latest version");
                        fileNeedsUpdate = false;
                    }
                    else
                    {
                        Console.WriteLine("Needs update");
                        XmlNode item = doc.GetElementsByTagName("item")[0]!;
                        string title = "N/A";
                        string creator = "N/A";
                        string description = "N/A";
                        string link = "N/A";
                        bool retweet = false;
                        foreach (XmlNode node in item.ChildNodes)
                        {
                        	if (node.Name == "title") title = node.InnerText.Trim();
                        	if (node.Name == "dc:creator")
                        	{
                        		creator = node.InnerText.Trim();
                        		if (creator.ToLower() != $"@{Environment.GetEnvironmentVariable("TWITTER_USER")}".ToLower())
                        		{
                        			retweet = true;
                        		}
                        	}
                        	if (node.Name == "description") description = node.InnerText.Trim();
                        	if (node.Name == "link") link = node.InnerText.Trim();
                        }
                        //Console.WriteLine(title);
                        //Console.WriteLine(creator);
                        //Console.WriteLine(description);
                        //Console.WriteLine(link);
                        if (!retweet)
                            Console.WriteLine((await PostDiscordMessage(client, creator, timestamp, title, link)).StatusCode);
                    }
                }
                else
                {
                    Console.WriteLine("File does not exist");
                }
                if (fileNeedsUpdate) File.WriteAllText("lastUpdatedValue", updated);
            }
        }

        private static async Task<HttpResponseMessage> PostDiscordMessage(HttpClient client, string author, long timestamp, string title, string link)
        {
            string url = $"{Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL")}?wait=true";
            WebhookData webhookData = new WebhookData
            {
                Username = WEBHOOK_USERNAME,
                AvatarUrl = WEBHOOK_AVATAR_URL,
                AllowedMentions = new Dictionary<string, string[]>{
                    { "parse", new string[0] }
                },
                Content = String.Format(CONTENT_FORMAT_STRING, author, timestamp, title, link)
            };
            string webhookJson = JsonSerializer.Serialize<WebhookData>(webhookData);
            StringContent content = new StringContent(webhookJson, Encoding.UTF8, "application/json");
            return await client.PostAsync(url, content);
        }
    }
}
