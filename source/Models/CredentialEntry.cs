using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Models
{
    public class CredentialEntry
    {
        public required string Domain { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public List<string> Groups { get; set; } = [];
    }
}