using AD_User_Reset_Print.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services.AD
{
    internal class SynchronizeUserService
    {
        public SynchronizeUserService() { }

        /// <summary>
        /// In a real application, this method would connect to Active Directory
        /// and fetch/synchronize user data. For this demo, it returns mock users.
        /// </summary>
        /// <returns>A list of User objects representing synchronized users.</returns>
        public List<User> Sync()
        {
            // For demo purposes, return a hardcoded list of users.
            // In a real scenario, you would implement AD querying logic here.
            return GetMockUsers();
        }

        /// <summary>
        /// Generates a list of mock User objects for demonstration purposes.
        /// </summary>
        /// <returns>A List of mock User objects.</returns>
        public static List<User> GetMockUsers()
        {
            return
            [
                new User("domain.com", "ajohnson", "Alice Johnson", "Alice", "Johnson", "alice@domain.com", "Manager", "IT Department", new List<string> { "Users", "IT Support" }),
                new User("domain.com", "bwilliams", "Bob Williams", "Bob", "Williams", "bob@domain.com", "Engineer", "R&D", new List<string> { "Users", "Engineers" }),
                new User("domain.com", "csmith", "Charlie Smith", "Charlie", "Smith", "charlie@domain.com", "Analyst", "Finance", new List<string> { "Users", "Finance" }),
                new User("domain.com", "davis", "Diana Davis", "Diana", "Davis", "diana@domain.com", "HR Specialist", "Human Resources", new List<string> { "Users", "HR" }),
                new User("domain.com", "efitz", "Eve Fitzgerald", "Eve", "Fitzgerald", "eve@domain.com", "Marketing Coordinator", "Marketing", new List<string> { "Users", "Marketing" }),
                new User("domain.com", "fgonzales", "Frank Gonzales", "Frank", "Gonzales", "frank@domain.com", "Software Developer", "IT Department", new List<string> { "Users", "Developers" })
            ];
        }
    }
}
