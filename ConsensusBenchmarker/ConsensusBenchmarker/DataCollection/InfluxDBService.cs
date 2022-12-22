using InfluxDB.Client;
using Microsoft.Extensions.Configuration;

namespace ConsensusBenchmarker.DataCollection
{
    public class InfluxDBService
    {
        private readonly string _token;
        private readonly string connectionString;

        public InfluxDBService(IConfiguration configuration)
        {
            _token = configuration.GetSection("InfluxDB").GetSection("Token").Value ?? throw new ApplicationException("InfluxDB token was not found or has no value");
            connectionString = configuration.GetSection("InfluxDB").GetSection("ConnectionString").Value ?? throw new ApplicationException("InfluxDB connection string was not found or has no value");
        }

        public void Write(Action<WriteApi> action)
        {
            using var client = new InfluxDBClient(connectionString, _token);
            using var write = client.GetWriteApi();
            action(write);
        }

        public async Task<T> QueryAsync<T>(Func<QueryApi, Task<T>> action)
        {
            using var client = new InfluxDBClient(connectionString, _token);
            var query = client.GetQueryApi();
            return await action(query);
        }
    }
}
