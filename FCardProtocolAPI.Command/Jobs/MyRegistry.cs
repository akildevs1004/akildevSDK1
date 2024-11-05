using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Jobs
{
    public class MyRegistry : FluentScheduler.Registry
    {
        public static RecordOptions Options { get; set; } = new RecordOptions();
        public MyRegistry(IConfiguration configuration)
        {
            configuration.GetSection("RecordOptions").Bind(Options);
            NonReentrantAsDefault();
            Schedule<ReadRecordJob>().ToRunNow().AndEvery(10).Seconds();
        }
    }
}
