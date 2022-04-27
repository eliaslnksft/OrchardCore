namespace OrchardCore.Security
{
    public class SecuritySettings
    {
        public string[] ContentSecurityPolicy { get; set; }

        public string ContentTypeOptions { get; set; }

        public string FrameOptions { get; set; }

        public string[] PermissionsPolicy { get; set; }

        public string ReferrerPolicy { get; set; }
    }
}