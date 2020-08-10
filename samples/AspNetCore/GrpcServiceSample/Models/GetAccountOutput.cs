using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcServiceSample.Models
{
    public class GetAccountOutput
    {
        public string Id { get; set; }
        public decimal Balance { get; set; }
    }
}
