using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Models
{
    public class GetRecordByIndexModel
    {
        public int TransactionType { get; set; }

        public int ReadIndex { get; set; }

        public int Quantity { get; set; }
    }
}
