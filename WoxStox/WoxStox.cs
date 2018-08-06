using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Wox.Plugin;

namespace WoxStox
{
    public enum Exchange
    {
        NASDAQ,
        TSE,
        NYSE
    }

    public class Main : IPlugin
    {

        public const string iconPath = @"images/logo.ico";
        public void Init(PluginInitContext context)
        {
            
        }

        public List<Result> Query (Query query)
        {
            ConcurrentQueue<Result> results = new ConcurrentQueue<Result>();
            if (query.Terms.Length < 2)
            {
                return null;
            }
            results.Enqueue(new Result()
            {
                Title = "TEST",
                IcoPath = iconPath,
                SubTitle = query.Terms[1]
            });
            List<Task> taskList = new List<Task>();
            foreach (Exchange ex in Enum.GetValues(typeof(Exchange)))
            {
                QueryInfo info = new QueryInfo
                {
                    Results = results,
                    Exchange = ex,
                    Symbol = query.Terms[1]
                };
                Task task = new Task(GetQuote, info);
                taskList.Add(task);
                task.Start();
            }
            Task.WaitAll(taskList.ToArray(), TimeSpan.FromSeconds(15));
            return results.ToList();
        }

        public void GetQuote(Object results)
        {
            QueryInfo info = results as QueryInfo;
            if (info == null) return;
            string url = GenerateUrl(info.Exchange, info.Symbol);
            using (HttpClient client = new HttpClient())
            {
                var response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    HttpContent responseContent = response.Content;
                    string responseString = responseContent.ReadAsStringAsync().Result;
                    ParseResponseContent(responseString, info.Results);
                }
                else
                {
                    info.Results.Enqueue(new Result()
                    {
                        Title = "ERROR",
                        IcoPath = iconPath,
                        SubTitle = response.StatusCode.ToString()
                    }
);
                }
            }
        }

        public void ParseResponseContent(string content, ConcurrentQueue<Result> results)
        {
            JObject response = JObject.Parse(content);
            try
            {
                // string symbol = response["Meta Data"]["2. Symbol"].ToString();
                JToken values = response.GetValue("Time Series (Daily)");
                JToken currentValue = values.First();
                string subtitle = string.Format("OPEN: {0} - HIGH: {1} - LOW: {2}",
                                                currentValue["1. open"].ToString(),
                                                currentValue["2. high"].ToString(),
                                                currentValue["3. low"].ToString());
                results.Enqueue(new Result() {
                    Title = String.Format("{0} : {1}", "symbol", currentValue["4. close"]),
                    IcoPath = iconPath,
                    SubTitle = subtitle
                    }
                );
            }
            catch (Exception ex)
            {
                results.Enqueue(new Result()
                {
                    Title = "ERROR",
                    IcoPath = iconPath,
                    SubTitle = ex.ToString()
                }
);
            }
        }

        private string GenerateUrl(Exchange ex, string symbol)
        {
            return String.Format(@"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={0}:{1}&apikey={2}",
                                  ex.ToString(),
                                  symbol,
                                  String.Empty);
        }
    }

    public class QueryInfo
    {
        public ConcurrentQueue<Result> Results;
        public Exchange Exchange;
        public string Symbol;
    }
}
