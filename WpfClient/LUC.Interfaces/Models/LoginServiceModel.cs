using System;
using System.Collections.Generic;

namespace LUC.Interfaces.Models
{
    public class LoginServiceModel
    {
        public LoginServiceModel()
        {
            Groups = new List<GroupServiceModel>();

            TenantId = String.Empty;
            Id = String.Empty;
            Token = String.Empty;
            Login = String.Empty;
        }

        public List<GroupServiceModel> Groups { get; set; }

        public String Token { get; set; }

        public String TenantId { get; set; }

        public String Login { get; set; }

        public String Id { get; set; }
    }
}
