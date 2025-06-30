using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Models
{
    public class ProgressReport
    {
        public int PercentComplete { get; set; }
        public string CurrentActivity { get; set; } = string.Empty;
    }
}