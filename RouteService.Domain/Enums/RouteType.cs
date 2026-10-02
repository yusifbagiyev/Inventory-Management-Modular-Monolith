namespace RouteService.Domain.Enums
{
    public enum RouteType
    {
        New=1,
        Existing=2,
        Update=3,
        Transfer= 4,
        Removal=5,
        /// <summary>The product's inventory code was changed, with the old and new code in the notes.</summary>
        CodeChange=6,
    }
}