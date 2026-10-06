namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>What the status page needs to explain a response that ended with an error status and no content.</summary>
    public class StatusPageViewModel
    {
        public int StatusCode { get; set; }

        /// <summary>Local address of the page the refused form was on, null when it is not known.</summary>
        public string? ReloadUrl { get; set; }
    }
}
