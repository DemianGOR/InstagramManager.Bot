using System;
using System.Collections.Generic;
using System.Text;

namespace InstagramManager.Data.Context
{
    public sealed class DataOptions
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
    }
}
