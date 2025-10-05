using ApiGateway.Dto;
using ApiGateway.Interfaces;

namespace ApiGateway.Security
{
    public class FileUploadRule : IWafRule
    {
        private static readonly string[] BlockedExtensions =
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".pif", ".scr",
            ".vbs", ".js", ".jar", ".zip", ".rar", ".sh", ".ps1"
        };

        public async Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            if (context.Request.HasFormContentType)
            {
                try
                {
                    var form = await context.Request.ReadFormAsync();
                    foreach (var file in form.Files)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (BlockedExtensions.Contains(extension))
                        {
                            return new WafValidationResult
                            {
                                IsValid = false,
                                Reason = $"Blocked file extension: {extension}",
                                BlockedBy = new List<string> { "FileUploadRule" }
                            };
                        }
                    }
                }
                catch
                {
                    // If form reading fails, allow request to proceed
                }
            }

            return new WafValidationResult { IsValid = true };
        }
    }
}