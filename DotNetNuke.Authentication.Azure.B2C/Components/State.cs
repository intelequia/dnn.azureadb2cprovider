﻿using System;

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    public class State
    {
        public State()
        {
            Service = AzureConfig.ServiceName;
            PortalId = -1;
            Culture = "";
            RedirectUrl = "";
            IsUserProfile = false;
            IsImpersonate = false;  
            IsResetPassword = false;
        }
        public State(string s)
        {
            Service = "";
            PortalId = -1;
            Culture = "";
            RedirectUrl = "";
            IsUserProfile = false;
            IsImpersonate = false;
            IsResetPassword = false;
            ParseState(s);
        }
        public string Service { get; set; }
        public int PortalId { get; set; }
        public string Culture { get; set; }
        public string RedirectUrl { get; set; }
        public bool IsUserProfile { get; set; }
        public bool IsImpersonate { get; set; }
        public bool IsResetPassword { get; set; }

        public override string ToString()
        {
            var s = "";
            if (!string.IsNullOrEmpty(Service))
            {
                s += $"s={Service}";
            }
            if (PortalId != -1)
            {
                s += $";p={PortalId}";
            }
            if (!string.IsNullOrEmpty(Culture))
            {
                s += $";c={Culture}";
            }
            if (!string.IsNullOrEmpty(RedirectUrl))
            {
                s += $";r={Uri.EscapeDataString(RedirectUrl)}";
            }
            if (IsUserProfile)
            {
                s += $";up=1";
            }
            if (IsImpersonate)
            {
                s += ";i=1";
            }
            if (IsResetPassword)
            {
                s += ";rp=1";
            }
            return s;
        }
        public void ParseState(string s)
        {
            if (string.IsNullOrEmpty(s))
                return;
            var pairs = s.Split(';');
            foreach (var pair in pairs)
            {
                var item = pair.Split('=');
                if (item.Length == 2)
                {
                    switch (item[0])
                    {
                        case "s":
                            Service = item[1];
                            break;
                        case "p":
                            PortalId = int.Parse(item[1]);
                            break;
                        case "c":
                            Culture = item[1];
                            break;
                        case "r":
                            RedirectUrl = Uri.UnescapeDataString(item[1]);
                            break;
                        case "up":
                            IsUserProfile = item[1] == "1";
                            break;
                        case "i":
                            IsImpersonate = item[1] == "1";
                            break;
                        case "rp":
                            IsResetPassword = item[1] == "1";
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}
