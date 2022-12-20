using InfluxDB.Client;
using Microsoft.Extensions.Configuration;

namespace ConsensusBenchmarker.DataCollection
{
    public class InfluxDBService
    {
        private readonly string _token;

        public InfluxDBService(IConfiguration configuration)
        {
            _token = configuration.GetSection("InfluxDB").GetSection("Token").Value ?? throw new ApplicationException("InfluxDB token was not found or has no value");
        }

        public void Write(Action<WriteApi> action)
        {
            using var client = new InfluxDBClient("http://192.168.100.200:8086", _token);
            using var write = client.GetWriteApi();
            action(write);
        }

        public async Task<T> QueryAsync<T>(Func<QueryApi, Task<T>> action)
        {
            using var client = new InfluxDBClient("http://192.168.100.200:8086", _token);
            var query = client.GetQueryApi();
            return await action(query);
        }
    }
}
