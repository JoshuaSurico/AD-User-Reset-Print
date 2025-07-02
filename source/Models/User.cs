namespace AD_User_Reset_Print.Models
{
    public class User
    {
        public string Domain { get; set; }
        public string SAMAccountName { get; set; }
        public string DisplayName { get; set; }
        public string GivenName { get; set; }
        public string Sn { get; set; }
        public string Mail { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> UserGroups { get; set; }

        public User()
        {
            // Parameterless constructor
        }

        public User(string domain, string sAMAccountName, string displayName, string givenName, string sn, string mail, string title, string description, List<string> userGroups)
        {
            Domain = domain;
            SAMAccountName = sAMAccountName;
            DisplayName = displayName;
            GivenName = givenName;
            Sn = sn;
            Mail = mail;
            Title = title;
            Description = description;
            UserGroups = userGroups;
        }

        public override string ToString()
        {
            return Domain + " | " + SAMAccountName + ", " + DisplayName; // what to show in the ListBox
        }
    }
}
