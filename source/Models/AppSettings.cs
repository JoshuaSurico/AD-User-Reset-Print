using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Models
{
    public static class AppSettings
    {
        // Define the single source of truth for the path here.
        public static readonly string UserListFilePath = Path.Combine("C:", "AD-User-Reset-Print", "UsersLists", "UserList.json");

        public static readonly string UserListDirectory = Path.Combine("C:", "AD-User-Reset-Print", "UsersLists");
    }
}
